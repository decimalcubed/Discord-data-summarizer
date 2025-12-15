using Newtonsoft.Json;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace DiscordDataSummarizer
{

    internal class MessageObject
    {

        public long ID;
        public DateTime Timestamp;
        public String Contents;
    }

    internal class Program
    {

        static void Main()
        {
            char separator = '\\';
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                separator = '/';
                // Get directory info from user
                Console.WriteLine("Please enter directory/path of unzipped discord data packet\nExample: /Downloads/package\nJust copy paste it or something");
            }
            else
            {
                // Get directory info from user
                Console.WriteLine("Please enter directory/path of unzipped discord data packet\nExample: C:\\Downloads\\package\nJust copy paste it or something");
            }

            string read_path = @"" + Console.ReadLine();
            if (!Directory.Exists(read_path))
            {

                Console.WriteLine("FAIL: Path does not exist\nMake sure you didn't leave a space at the end\nPress enter to exit.");
                Console.ReadLine(); // Wait for input
                throw new Exception("Path doesnt exist");
            }
            else if (!File.Exists(read_path + "" + separator + "messages" + separator + "index.json")) // Ensure path has messages and index.json
            {

                Console.WriteLine("FAIL: Path does not contain 'messages" + separator + "index.json', this may not be a data packet, or it may be missing messages\npress enter to exit.");
                Console.ReadLine(); // Wait for input
                throw new Exception("Path not data packet");
            }

            // Get debug info
            Console.WriteLine("Show extra debug info? Y/N");
            string read = @"" + Console.ReadLine(); // Worlds worst implementation idk how ppl normally do this
            bool show_extra_debug = (read) == "Y" || (read == "y");

            DoStuff(read_path, show_extra_debug, separator);
        }

        static void DoStuff(string dir, bool show_extra_debug, char separator)
        {

            int MessagesIn2025 = 0;
            int MessagesIn2024 = 0;
            int MessagesIn2023 = 0;
            int MessagesIn2022 = 0;
            int MessagesIn2021 = 0;
            int MessagesIn2020 = 0;

            SortedDictionary<long, long> MessagesPerChannel2025 = new SortedDictionary<long, long>();
            SortedDictionary<long, long> MessagesPerChannel2024 = new SortedDictionary<long, long>();
            SortedDictionary<long, long> MessagesPerChannel2023 = new SortedDictionary<long, long>();
            SortedDictionary<long, long> MessagesPerChannel2022 = new SortedDictionary<long, long>();
            SortedDictionary<long, long> MessagesPerChannel2021 = new SortedDictionary<long, long>();
            SortedDictionary<long, long> MessagesPerChannel2020 = new SortedDictionary<long, long>();

            Console.WriteLine("Processing messages, this may take a while");

            StreamReader shitr = new StreamReader(dir + "" + separator + "messages" + separator + "index.json");
            string jsonr = shitr.ReadToEnd();
            SortedDictionary<long, string> channel_map = JsonConvert.DeserializeObject<SortedDictionary<long, string>>(jsonr);

            if (channel_map == null)
            {

                Console.WriteLine("FAIL: Something went really wrong, and the program was unable to parse " + dir + "" + separator + "messages" + separator + "index.json\nprocessing cannot continue without this, please try again or make a github issue or something idk if u really want\nPress enter to exit.");
                Console.ReadLine(); // Wait for input
                throw new Exception("Bad index");
            }

            // Do stuff here
            foreach (string path in Directory.GetDirectories(dir + "" + separator + "messages"))
            {

                // Check to see if its a folder
                string shitass = Path.GetFileName(path).Substring(1);
                long channel_id = long.Parse(shitass);

                MessagesPerChannel2025[channel_id] = 0;
                MessagesPerChannel2024[channel_id] = 0;
                MessagesPerChannel2023[channel_id] = 0;
                MessagesPerChannel2022[channel_id] = 0;
                MessagesPerChannel2021[channel_id] = 0;
                MessagesPerChannel2020[channel_id] = 0;

                if (show_extra_debug)
                {

                    Console.WriteLine(channel_id.ToString() + ": " + channel_map[channel_id]);
                }
                
                using (StreamReader r = new StreamReader(path + "" + separator + "messages.json"))
                {

                    string json = r.ReadToEnd();
                    List<MessageObject> result = JsonConvert.DeserializeObject<List<MessageObject>>(json);
                    if (result == null)
                    {

                        Console.WriteLine("FAIL: Something went wrong parsing file: " + path + "" + separator + "messages.json\nPress enter to exit.");
                        Console.ReadLine(); // Wait for input
                        throw new Exception("Bad message");
                    }

                    foreach (MessageObject msg in result.ToArray())
                    {

                        if (msg.Timestamp.Year == 2025)
                        {

                            //Console.WriteLine(msg.Contents);
                            MessagesIn2025 += 1;
                            MessagesPerChannel2025[channel_id] += 1;
                        }
                        else if (msg.Timestamp.Year == 2024)
                        {

                            //Console.WriteLine(msg.Contents);
                            MessagesIn2024 += 1;
                            MessagesPerChannel2024[channel_id] += 1;
                        }
                        else if (msg.Timestamp.Year == 2023)
                        {

                            //Console.WriteLine(msg.Contents);
                            MessagesIn2023 += 1;
                            MessagesPerChannel2023[channel_id] += 1;
                        }
                        else if (msg.Timestamp.Year == 2022)
                        {

                            //Console.WriteLine(msg.Contents);
                            MessagesIn2022 += 1;
                            MessagesPerChannel2022[channel_id] += 1;
                        }
                        else if (msg.Timestamp.Year == 2021)
                        {

                            //Console.WriteLine(msg.Contents);
                            MessagesIn2021 += 1;
                            MessagesPerChannel2021[channel_id] += 1;
                        }
                        else if(msg.Timestamp.Year == 2020)
                        {

                            //Console.WriteLine(msg.Contents);
                            MessagesIn2020 += 1;
                            MessagesPerChannel2020[channel_id] += 1;
                        }
                        // Console.WriteLine(msg.Timestamp.Year);
                        //Console.WriteLine(msg.Contents);
                    }
                }
            }

            int maxlength = 0;
            int numberoftops = 5;//
            Console.WriteLine("Number of top conversations to display: (default: 5)");
            string temp = Console.ReadLine();
            int.TryParse(temp, out numberoftops);
            if (numberoftops <= 0)
            {
                numberoftops = 5;
            }

            //sorry for this :3
            if (maxlength < MessagesIn2025.ToString().Length)
            {
                maxlength = MessagesIn2025.ToString().Length;
            }
            if (maxlength < MessagesIn2024.ToString().Length)
            {
                maxlength = MessagesIn2024.ToString().Length;
            }
            if (maxlength < MessagesIn2023.ToString().Length)
            {
                maxlength = MessagesIn2023.ToString().Length;
            }
            if (maxlength < MessagesIn2022.ToString().Length)
            {
                maxlength = MessagesIn2022.ToString().Length;
            }
            if (maxlength < MessagesIn2021.ToString().Length)
            {
                maxlength = MessagesIn2021.ToString().Length;
            }
            if (maxlength < MessagesIn2020.ToString().Length)
            {
                maxlength = MessagesIn2020.ToString().Length;
            }

            // Add gaps between debug info
            Console.WriteLine("\n\n\n");

            Console.WriteLine("this many messages were in 2025: " + MessagesIn2025.ToString().PadLeft(maxlength, ' '));
            Console.WriteLine("this many messages were in 2024: " + MessagesIn2024.ToString().PadLeft(maxlength, ' '));
            Console.WriteLine("this many messages were in 2023: " + MessagesIn2023.ToString().PadLeft(maxlength, ' '));
            Console.WriteLine("this many messages were in 2022: " + MessagesIn2022.ToString().PadLeft(maxlength, ' '));
            Console.WriteLine("this many messages were in 2021: " + MessagesIn2021.ToString().PadLeft(maxlength, ' '));
            Console.WriteLine("this many messages were in 2020: " + MessagesIn2020.ToString().PadLeft(maxlength, ' '));

            //padding may be too much here, but i'm too lazy to fix it
            var max_2025 = MessagesPerChannel2025.OrderByDescending(d => d.Value).ToArray();
            Console.WriteLine("most popular channels in 2025:\n");
            for (int i = 0; i < numberoftops; i++)
            {
                if (max_2025[i].Value == 0)
                {
                    break;
                }
                Console.WriteLine("#" + (i+1).ToString().PadLeft(numberoftops.ToString().Length, ' ') + ": " + max_2025[i].Value.ToString().PadLeft(maxlength, ' ') + " | " + channel_map[max_2025[i].Key]);
            }
            Console.WriteLine();

            var max_2024 = MessagesPerChannel2024.OrderByDescending(d => d.Value).ToArray();
            Console.WriteLine("most popular channels in 2024:\n");
            for (int i = 0; i < numberoftops; i++)
            {
                if (max_2024[i].Value == 0)
                {
                    break;
                }
                Console.WriteLine("#" + (i+1).ToString().PadLeft(numberoftops.ToString().Length, ' ') + ": " + max_2024[i].Value.ToString().PadLeft(maxlength, ' ') + " | " + channel_map[max_2024[i].Key]);
            }
            Console.WriteLine();

            var max_2023 = MessagesPerChannel2023.OrderByDescending(d => d.Value).ToArray();
            Console.WriteLine("most popular channels in 2023:\n");
            for (int i = 0; i < numberoftops; i++)
            {
                if (max_2023[i].Value == 0)
                {
                    break;
                }
                Console.WriteLine("#" + (i+1).ToString().PadLeft(numberoftops.ToString().Length, ' ') + ": " + max_2023[i].Value.ToString().PadLeft(maxlength, ' ') + " | " + channel_map[max_2023[i].Key]);
            }
            Console.WriteLine();

            var max_2022 = MessagesPerChannel2022.OrderByDescending(d => d.Value).ToArray();
            Console.WriteLine("most popular channels in 2022:\n");
            for (int i = 0; i < numberoftops; i++)
            {
                if (max_2022[i].Value == 0)
                {
                    break;
                }
                Console.WriteLine("#" + (i+1).ToString().PadLeft(numberoftops.ToString().Length, ' ') + ": " + max_2022[i].Value.ToString().PadLeft(maxlength, ' ') + " | " + channel_map[max_2022[i].Key]);
            }
            Console.WriteLine();

            var max_2021 = MessagesPerChannel2021.OrderByDescending(d => d.Value).ToArray();
            Console.WriteLine("most popular channels in 2021:\n");
            for (int i = 0; i < numberoftops; i++)
            {
                if (max_2021[i].Value == 0)
                {
                    break;
                }
                Console.WriteLine("#" + (i+1).ToString().PadLeft(numberoftops.ToString().Length, ' ') + ": " + max_2021[i].Value.ToString().PadLeft(maxlength, ' ') + " | " + channel_map[max_2021[i].Key]);
            }
            Console.WriteLine();

            var max_2020 = MessagesPerChannel2020.OrderByDescending(d => d.Value).ToArray();
            Console.WriteLine("most popular channels in 2020:\n");
            for (int i = 0; i < numberoftops; i++)
            {
                if (max_2020[i].Value == 0)
                {
                    break;
                }
                Console.WriteLine("#" + (i+1).ToString().PadLeft(numberoftops.ToString().Length, ' ') + ": " + max_2020[i].Value.ToString().PadLeft(maxlength, ' ') + " | " + channel_map[max_2020[i].Key]);
            }
            Console.WriteLine();

            Console.WriteLine("Finished, scroll up to see summarized data");
        }
    }
}
