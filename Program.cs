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

            // Get directory info from user
            Console.WriteLine("Please enter directory/path of unzipped discord data packet\nExample: C:\\Downloads\\package\nJust copy paste it or something");
            string read_path = @"" + Console.ReadLine();
            if (!Directory.Exists(read_path))
            {

                throw new Exception("Path does not exist");
            }
            else if (!File.Exists(read_path + "\\messages\\index.json")) // Ensure path has messages and index.json
            {

                throw new Exception("Path does not contain 'messages\\index.json', this may not be a data packet, or it may be missing messages, please try again.");
            }

            // Get debug info
            Console.WriteLine("Show extra debug info? Y/N");
            string read = @"" + Console.ReadLine(); // Worlds worst implementation idk how ppl normally do this
            bool show_extra_debug = (read) == "Y" || (read == "y");

            DoStuff(read_path, show_extra_debug);
        }

        static void DoStuff(string dir, bool show_extra_debug)
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

            StreamReader shitr = new StreamReader(dir + "\\messages\\index.json");
            string jsonr = shitr.ReadToEnd();
            SortedDictionary<long, string> channel_map = JsonConvert.DeserializeObject<SortedDictionary<long, string>>(jsonr);

            if (channel_map == null)
            {

                throw new Exception("Something went really wrong, and the program was unable to parse " + dir + "\\messages\\index.json\nprocessing cannot continue without this, please try again or make a github issue or something idk if u really want");
            }

            // Do stuff here
            foreach (string path in Directory.GetDirectories(dir + "\\messages"))
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
                
                using (StreamReader r = new StreamReader(path + "\\messages.json"))
                {

                    string json = r.ReadToEnd();
                    List<MessageObject> result = JsonConvert.DeserializeObject<List<MessageObject>>(json);
                    if (result == null)
                    {

                        throw new Exception("Something went wrong parsing file: " + path + "\\messages.json\nThe script will ");
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

            // Add gaps between debug info
            Console.WriteLine("\n\n\n");

            Console.WriteLine("this many messages were in 2025: " + MessagesIn2025.ToString());
            Console.WriteLine("this many messages were in 2024: " + MessagesIn2024.ToString());
            Console.WriteLine("this many messages were in 2023: " + MessagesIn2023.ToString());
            Console.WriteLine("this many messages were in 2022: " + MessagesIn2022.ToString());
            Console.WriteLine("this many messages were in 2021: " + MessagesIn2021.ToString());
            Console.WriteLine("this many messages were in 2020: " + MessagesIn2020.ToString());

            Console.WriteLine("\n");
            Console.WriteLine("WARN!: if there were no messages in a year, it might show random channels here but idk what it does it might just be wrong\n");

            var max_2025 = MessagesPerChannel2025.OrderByDescending(d => d.Value).ToArray();
            Console.WriteLine("most popular channels in 2025: \n    "
                + channel_map[max_2025[0].Key] + " \n    "
                + channel_map[max_2025[1].Key] + " \n    "
                + channel_map[max_2025[2].Key] + " \n    "
                + channel_map[max_2025[3].Key] + " \n    "
                + channel_map[max_2025[4].Key] + " \n ");

            var max_2024 = MessagesPerChannel2024.OrderByDescending(d => d.Value).ToArray();
            Console.WriteLine("most popular channels in 2024: \n    "
                + channel_map[max_2024[0].Key] + " \n    "
                + channel_map[max_2024[1].Key] + " \n    "
                + channel_map[max_2024[2].Key] + " \n    "
                + channel_map[max_2024[3].Key] + " \n    "
                + channel_map[max_2024[4].Key] + " \n ");

            var max_2023 = MessagesPerChannel2023.OrderByDescending(d => d.Value).ToArray();
            Console.WriteLine("most popular channels in 2023: \n    "
                + channel_map[max_2023[0].Key] + " \n    "
                + channel_map[max_2023[1].Key] + " \n    "
                + channel_map[max_2023[2].Key] + " \n    "
                + channel_map[max_2023[3].Key] + " \n    "
                + channel_map[max_2023[4].Key] + " \n ");

            var max_2022 = MessagesPerChannel2022.OrderByDescending(d => d.Value).ToArray();
            Console.WriteLine("most popular channels in 2022: \n    "
                + channel_map[max_2022[0].Key] + " \n    "
                + channel_map[max_2022[1].Key] + " \n    "
                + channel_map[max_2022[2].Key] + " \n    "
                + channel_map[max_2022[3].Key] + " \n    "
                + channel_map[max_2022[4].Key] + " \n ");

            var max_2021 = MessagesPerChannel2021.OrderByDescending(d => d.Value).ToArray();
            Console.WriteLine("most popular channels in 2021: \n    "
                + channel_map[max_2021[0].Key] + " \n    "
                + channel_map[max_2021[1].Key] + " \n    "
                + channel_map[max_2021[2].Key] + " \n    "
                + channel_map[max_2021[3].Key] + " \n    "
                + channel_map[max_2021[4].Key] + " \n ");

            var max_2020 = MessagesPerChannel2020.OrderByDescending(d => d.Value).ToArray();
            Console.WriteLine("most popular channels in 2020: \n    "
                + channel_map[max_2020[0].Key] + " \n    "
                + channel_map[max_2020[1].Key] + " \n    "
                + channel_map[max_2020[2].Key] + " \n    "
                + channel_map[max_2020[3].Key] + " \n    "
                + channel_map[max_2020[4].Key] + " \n");
        }
    }
}
