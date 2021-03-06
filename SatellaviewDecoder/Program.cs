﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace SatellaviewDecoder
{
    enum data
    {
        time = 0,
        nrz = 1,
        clock = 2,
        fsync = 3,
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SatellaviewDecoder v0.6");
            Console.WriteLine("by LuigiBlood");

            if (args.Length == 0)
            {
                Console.WriteLine("\nUsage: EXE <option> <csv file>");
                Console.WriteLine("Options:");
                Console.WriteLine("-i   : Output interleaved frame files (Default, optional)");
                Console.WriteLine("-d   : Output deinterleaved frame files");
                Console.WriteLine("-ra  : Output RAW audio file");
                Console.WriteLine("-wav : Output WAV audio file");
            }
            else if (args.Length == 1)
            {
                Extract(args[0], 0);
            }
            else
            {
                if (args[0] == "-i")
                {
                    Extract(args[1], 0);
                }
                else if (args[0] == "-d")
                {
                    Extract(args[1], 1);
                }
                else if (args[0] == "-ra")
                {
                    Extract(args[1], 2);
                }
                else if (args[0] == "-wav")
                {
                    Extract(args[1], 3);
                }
                else
                {
                    Console.WriteLine("Unknown option");
                }
            }


        }

        static void Extract(string filename, int action)
        {
            bool lastFS = false;
            bool lastCLK = false;
            bool isModeB = false;
            List<bool> bitstream = new List<bool>();
            List<byte> output = new List<byte>();
            using (StreamReader csv = File.OpenText(filename))
            {
                csv.ReadLine(); //skip csv names
                {
                    string[] curline = csv.ReadLine().Split(',');
                    lastFS = curline[(int)data.fsync] == " 1";
                    lastCLK = curline[(int)data.clock] == " 1";
                }

                //Skip
                while (lastFS)
                {
                    string[] curline = csv.ReadLine().Split(',');
                    lastFS = curline[(int)data.fsync] == " 1";
                    lastCLK = curline[(int)data.clock] == " 1";
                }
                //Find the first frame
                while (!lastFS)
                {
                    string[] curline = csv.ReadLine().Split(',');
                    lastFS = curline[(int)data.fsync] == " 1";
                    lastCLK = curline[(int)data.clock] == " 1";
                }

                //At first frame
                int frameCount = 0;
                while (!csv.EndOfStream)
                {
                    string[] curline = csv.ReadLine().Split(',');
                    if (lastFS == (curline[(int)data.fsync] == " 1") && curline[(int)data.clock] == " 0" && lastCLK)
                    {
                        bitstream.Add(curline[(int)data.nrz] == " 1");
                        //Console.WriteLine(curline[1] + curline[2] + curline[3]);

                        if (bitstream.Count == 2048)
                        {
                            //Descramble
                            descramble(bitstream);

                            //Check Errors
                            checkFEC(bitstream, frameCount);

                            if (action == 0)
                            {
                                //Interleaved
                                outputToBytes(bitstream, output);
                            }
                            else if (action >= 1)
                            {
                                //Deinterleaved
                                List<bool> deinterleaved = new List<bool>();

                                deinterleaved.AddRange(bitstream.GetRange(0, 64));  //First 64 bits are already deinterleaved (frame sync, control, range, spare)

                                isModeB = bitstream[16];    //Get current mode

                                int amountBitsAudio = 10;   //Mode A
                                int amountBitsData = 15;
                                if (isModeB)
                                {
                                    amountBitsAudio = 16;   //Mode B
                                    amountBitsData = 7;
                                }

                                //Ignoring range and spare
                                //deinterleave(bitstream, deinterleaved, 64, 62);

                                if (!isModeB)
                                {
                                    //Mode A bitstream

                                    //get Audio - format is alternating sound channels in this case
                                    deinterleave(bitstream, deinterleaved, 64, amountBitsAudio * 4);

                                    //get Data
                                    deinterleave(bitstream, deinterleaved, (64 + (32 * amountBitsAudio * 4)), amountBitsData);

                                    //get FEC
                                    deinterleave(bitstream, deinterleaved, (64 + (32 * amountBitsAudio * 4) + (amountBitsData * 32)), 7);
                                }
                                else
                                {
                                    //Mode B bitstream

                                    //get Audio
                                    deinterleave(bitstream, deinterleaved, 64, amountBitsAudio * 3);

                                    //get Data
                                    deinterleave(bitstream, deinterleaved, (64 + (32 * amountBitsAudio * 3)), amountBitsData);

                                    //get FEC
                                    deinterleave(bitstream, deinterleaved, (64 + (32 * amountBitsAudio * 3) + (amountBitsData * 32)), 7);
                                }
                                

                                if (action == 1)
                                {
                                    //Deinterleaved RAW frame output
                                    outputToBytes(deinterleaved, output);
                                }
                                else if (action >= 2)
                                {
                                    //Deinterleaved Audio output
                                    List<bool> outputTemp = new List<bool>();

                                    if (isModeB)
                                    {
                                        //Mode B - copy as is
                                        outputTemp.AddRange(deinterleaved.GetRange(64, amountBitsAudio * 3 * 32));
                                    }
                                    else
                                    {
                                        //Mode A - convert 10 bit samples to 16 bit with padding
                                        for (int i = 0; i < 32 * 4; i++)
                                        {
                                            outputTemp.AddRange(deinterleaved.GetRange(64 + (amountBitsAudio * i), amountBitsAudio));
                                            outputTemp.Add(false);
                                            outputTemp.Add(false);
                                            outputTemp.Add(false);
                                            outputTemp.Add(false);
                                            outputTemp.Add(false);
                                            outputTemp.Add(false);
                                        }
                                    }
                                    outputToBytes(outputTemp, output);
                                }
                            }
                            bitstream.Clear();
                            frameCount++;
                        }
                    }

                    lastFS = curline[(int)data.fsync] == " 1";
                    lastCLK = curline[(int)data.clock] == " 1";
                }

                Console.WriteLine(frameCount + " frames found.");

                //Output
                string ext = "";
                if (action == 0)
                    ext = ".int";
                else if (action == 1)
                    ext = ".din";
                else if (action == 2)
                    ext = ".raw";
                else if (action == 3)
                    ext = ".wav";

                using (FileStream outputfile = File.Create(filename + ext))
                {
                    if (action == 3)
                    {
                        //WAV file output
                        int total_size = output.Count + 44;

                        byte[] rifx_header_a =
                        {
                            0x52, 0x49, 0x46, 0x46,     //RIFF, little endian
                            (byte)((total_size >> 0) & 0xFF),(byte)((total_size >> 8) & 0xFF),(byte)((total_size >> 16) & 0xFF),(byte)((total_size >> 24) & 0xFF),

                            0x57, 0x41, 0x56, 0x45,     //WAVE

                            0x66, 0x6D, 0x74, 0x20,     //fmt
                            0x10, 0x00, 0x00, 0x00,     //Chunk Size (16 bytes)
                            0x01, 0x00,                 //PCM Format
                            0x04, 0x00,                 //4 tracks
                            0x00, 0x7D, 0x00, 0x00,     //32000 Hz
                            0x00, 0xE8, 0x03, 0x00,     //256000 B/s
                            0x08, 0x00,                 //Block Align
                            0x10, 0x00,                 //16-bit samples

                            0x64, 0x61, 0x74, 0x61,     //data
                            (byte)((output.Count >> 0) & 0xFF),(byte)((output.Count >> 8) & 0xFF),(byte)((output.Count >> 16) & 0xFF),(byte)((output.Count >> 24) & 0xFF)
                        };

                        byte[] rifx_header_b =
                        {
                            0x52, 0x49, 0x46, 0x46,     //RIFF, little endian
                            (byte)((total_size >> 0) & 0xFF),(byte)((total_size >> 8) & 0xFF),(byte)((total_size >> 16) & 0xFF),(byte)((total_size >> 24) & 0xFF),

                            0x57, 0x41, 0x56, 0x45,     //WAVE

                            0x66, 0x6D, 0x74, 0x20,     //fmt
                            0x10, 0x00, 0x00, 0x00,     //Chunk Size (16 bytes)
                            0x01, 0x00,                 //PCM Format
                            0x02, 0x00,                 //Stereo
                            0x80, 0xBB, 0x00, 0x00,     //48000 Hz
                            0x00, 0xEE, 0x02, 0x00,     //192000 B/s
                            0x04, 0x00,                 //Block Align
                            0x10, 0x00,                 //16-bit samples

                            0x64, 0x61, 0x74, 0x61,     //data
                            (byte)((output.Count >> 0) & 0xFF),(byte)((output.Count >> 8) & 0xFF),(byte)((output.Count >> 16) & 0xFF),(byte)((output.Count >> 24) & 0xFF)
                        };

                        if (!isModeB)
                            outputfile.Write(rifx_header_a, 0, rifx_header_a.Length);
                        else
                            outputfile.Write(rifx_header_b, 0, rifx_header_b.Length);

                        for (int i = 0; i < output.Count; i++)
                        {
                            outputfile.WriteByte(output[i ^ 1]);
                        }
                    }
                    else
                    {
                        //RAW output
                        outputfile.Write(output.ToArray(), 0, output.Count);
                    }
                }
            }
        }

        static void outputToBytes(List<bool> input, List<byte> output)
        {
            byte temp = 0;
            for (int i = 0; i < input.Count; i++)
            {
                if (i % 8 == 0)
                {
                    if (i > 0)
                    {
                        output.Add(temp);
                    }

                    temp = (byte)(Convert.ToByte(input[i]) << (7 - (i % 8)));
                }
                else
                {
                    temp |= (byte)(Convert.ToByte(input[i]) << (7 - (i % 8)));
                }
            }
            output.Add(temp);
        }

        static void descramble(List<bool> bitstream)
        {
            //full 2048 bit frame as input
            List<bool> shift = new List<bool>();
            for (int i = 0; i < 10; i++)
                shift.Add(true);

            for (int i = 16; i < bitstream.Count; i++)
            {
                bitstream[i] = shift[9] ^ bitstream[i];
                shift.Insert(0, shift[6] ^ shift[9]);
                shift.RemoveAt(10);
            }
        }

        static void checkFEC(List<bool> bitstream, int frame)
        {
            //full 2048 bit frame descrambled as input
            for (int i = 0; i < 32; i++)
            {
                byte shift = 0;     //7-bit Shift register

                for (int j = 0; j < 56; j++)
                {
                    bool test = bitstream[32 + i + (j * 32)];                   //data.bit
                    bool shift0 = ((shift & 1) == 1);                           //shift.bit0 (before shift)
                    shift >>= 1;                                                //shift >> 1 (it does not rotate)
                    shift ^= (byte)(Convert.ToByte(shift0 ^ test) << 6);        //shift.bit6 ^= shift0 ^ data.bit
                    shift ^= (byte)(Convert.ToByte(shift0 ^ test) << 4);        //shift.bit4 ^= shift0 ^ data.bit
                    shift ^= (byte)(Convert.ToByte(shift0 ^ test) << 0);        //shift.bit0 ^= shift0 ^ data.bit
                }

                byte checkFEC = 0;
                for (int j = 0; j < 7; j++)
                {
                    checkFEC |= (byte)(Convert.ToByte(bitstream[32 + 32 * 56 + i + (j * 32)]) << j);
                }

                if (checkFEC != shift)
                {
                    Console.WriteLine(frame + ": FEC wrong (" + (i + 1) + "/32) + check 0x" + checkFEC.ToString("X02") + " vs calc 0x" + shift.ToString("X02"));
                }
                else
                {
                    //Console.WriteLine(frame + ": FEC correct (" + (i + 1) + "/32) + check 0x" + checkFEC.ToString("X02") + " vs calc 0x" + shift.ToString("X02"));
                }
            }
        }

        static void deinterleave(List<bool> bitstream, List<bool> deinterleaved, int start, int count)
        {
            for (int i = 0; i < 32; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    deinterleaved.Add(bitstream[start + i + j * 32]);
                }
            }
        }
    }
}
