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
            Console.WriteLine("SatellaviewDecoder v0.1");
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
                            if (action == 0)
                            {
                                //Interleaved
                                outputToBytes(bitstream, output);
                            }
                            else if (action >= 1)
                            {
                                //Deinterleaved
                                List<bool> deinterleaved = new List<bool>();

                                deinterleaved.AddRange(bitstream.GetRange(0, 32));

                                for (int i = 0; i < 63; i++)
                                {
                                    for (int smp = 0; smp < 32; smp++)
                                    {
                                        deinterleaved.Add(bitstream[32 + (smp * 63) + i]);
                                    }
                                }

                                if (action == 1)
                                {
                                    //Output to bytes
                                    outputToBytes(deinterleaved, output);
                                }
                                else
                                {
                                    bool isModeA = deinterleaved[16];
                                    List<bool> outputTemp = new List<bool>();

                                    if (action == 2)
                                    {
                                        int amountBitsAudio = 16; //Mode B
                                        if (isModeA)
                                            amountBitsAudio = 10;

                                        outputTemp.AddRange(deinterleaved.GetRange(64, amountBitsAudio * 32));
                                    }
                                    else
                                    {
                                        //Only Mode B
                                        outputTemp.AddRange(deinterleaved.GetRange(1600, 30));
                                        outputTemp.Add(false);
                                        outputTemp.Add(false);
                                        for (int i = 0; i < 224; i++)
                                        {
                                            if (i < 176)
                                                outputTemp.Add(deinterleaved[1600 + 30 + i]);
                                            else
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
    }
}
