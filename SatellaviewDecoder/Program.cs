using System;
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
            Console.WriteLine("SatellaviewDecoder v0.2");
            Console.WriteLine("by LuigiBlood");

            if (args.Length == 0)
            {
                Console.WriteLine("\nUsage: EXE <option> <csv file>");
                Console.WriteLine("Options:");
                Console.WriteLine("-i : Output interleaved frame files (Default, optional)");
                Console.WriteLine("-d : Output deinterleaved frame files");
                Console.WriteLine("-ra : Output RAW audio file");
                Console.WriteLine("-rd : Output RAW data file");
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
                else if (args[0] == "-rd")
                {
                    Extract(args[1], 3);
                }
            }


        }

        static void Extract(string filename, int action)
        {
            bool lastFS = false;
            bool lastCLK = false;
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

                            if (action == 0)
                            {
                                //Interleaved
                                outputToBytes(bitstream, output);
                            }
                            else if (action >= 1)
                            {
                                //Deinterleaved
                                List<bool> deinterleaved = new List<bool>();

                                deinterleaved.AddRange(bitstream.GetRange(0, 64));


                                //Output to bytes
                                bool isModeB = bitstream[16];

                                int amountBitsAudio = 10; //Mode A
                                int amountBitsData = 15;
                                if (isModeB)
                                {
                                    amountBitsAudio = 16;
                                    amountBitsData = 7;
                                }

                                //get Audio 1/2
                                for (int i = 0; i < 32; i++)
                                {
                                    for (int j = 0; j < amountBitsAudio; j++)
                                    {
                                        deinterleaved.Add(bitstream[64 + i + j * 32]);
                                    }
                                }

                                //get Audio 2/1
                                for (int i = 0; i < 32; i++)
                                {
                                    for (int j = 0; j < amountBitsAudio; j++)
                                    {
                                        deinterleaved.Add(bitstream[64 + (32 * amountBitsAudio) + i + j * 32]);
                                    }
                                }

                                //get Audio 1/2
                                for (int i = 0; i < 32; i++)
                                {
                                    for (int j = 0; j < amountBitsAudio; j++)
                                    {
                                        deinterleaved.Add(bitstream[64 + (32 * amountBitsAudio * 2) + i + j * 32]);
                                    }
                                }

                                if (!isModeB)
                                {
                                    //get Audio 4
                                    for (int i = 0; i < 32; i++)
                                    {
                                        for (int j = 0; j < amountBitsAudio; j++)
                                        {
                                            deinterleaved.Add(bitstream[64 + (32 * amountBitsAudio * 2) + i + j * 32]);
                                        }
                                    }
                                }

                                //get Data
                                for (int i = 0; i < 32; i++)
                                {
                                    for (int j = 0; j < amountBitsData; j++)
                                    {
                                        deinterleaved.Add(bitstream[64 + (32 * amountBitsAudio * 3) + i + j * 32]);
                                    }
                                }

                                //get FEC
                                for (int i = 0; i < 32; i++)
                                {
                                    for (int j = 0; j < 7; j++)
                                    {
                                        deinterleaved.Add(bitstream[64 + (32 * amountBitsAudio * 3) + (amountBitsData * 32) + i + j * 32]);
                                    }
                                }

                                if (action == 1)
                                {
                                    outputToBytes(deinterleaved, output);
                                }
                                else if (action == 2)
                                {
                                    List<bool> outputTemp = new List<bool>();

                                    if (isModeB)
                                    {
                                        for (int i = 0; i < amountBitsAudio; i++)
                                        {
                                            //Stereo Output
                                            outputTemp.AddRange(deinterleaved.GetRange(64 + (32 * i), amountBitsAudio));                                    //L
                                            outputTemp.AddRange(deinterleaved.GetRange(64 + (amountBitsAudio * 32) + (32 * i), amountBitsAudio));           //R

                                            outputTemp.AddRange(deinterleaved.GetRange(64 + (amountBitsAudio * 32 * 2) + (32 * i), amountBitsAudio));       //L
                                            outputTemp.AddRange(deinterleaved.GetRange(64 + (32 * i) + 16, amountBitsAudio));                               //R

                                            outputTemp.AddRange(deinterleaved.GetRange(64 + (amountBitsAudio * 32) + (32 * i) + 16, amountBitsAudio));      //L
                                            outputTemp.AddRange(deinterleaved.GetRange(64 + (amountBitsAudio * 32 * 2) + (32 * i) + 16, amountBitsAudio));  //R
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
                    ext = ".dat";

                using (FileStream outputfile = File.Create(filename + ext))
                {
                    outputfile.Write(output.ToArray(), 0, output.Count);
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
    }
}
