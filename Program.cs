using Microsoft.VisualBasic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ScottPlot;
using ScottPlot.Interactivity;
using ScottPlot.Plottables;
using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace DiscordDataSummarizerVC2
{

    internal class Program
    {

        static internal Boolean DO_ANONYMIZE_GUIDS = false;
        static string AnonymizeGuildOrChannelId(long? guid)
        {

            if (DO_ANONYMIZE_GUIDS)
            {

                return "[Channel/Guild id hidden]";
            }

            if (guid != null)
            {

                return guid.ToString();
            }

            return "_";
        }

        internal enum ChannelType
        {

            GuildGeneric, // Text + voice channels

            Groupchat,
            UserDM,

            Other_Unhandled, // Completely unhandled cases
            Other_Unknown, // Handled cases, but we dont know what exactly they belong to
            Other_GuildUnknown // Forums, stages, announcement channels, etc
        }

        static internal long CUTOFF_YEAR = 123; // Dont analyze any data from BEFORE this year
        static internal long CUTOFF_FILE_LINE = 800000000; // Dont analyze any data after this line

        // Caches used in a few places
        static Dictionary<long, ChannelType> ChannelTypeCache = new Dictionary<long, ChannelType>();
        static SortedDictionary<long, long> MetaData_ChannelIdToGuildIdMap = new SortedDictionary<long, long>();

        internal class ChannelMetaData
        {

            public string type = "unknown"; // DM | GROUP_DM | GUILD_TEXT
        }

        internal class classification
        {

            public string event_type = "unknown";
            public Nullable<long> channel_id = null;
            public Nullable<long> guild_id = null;
        }

        internal class ChannelData
        {

            //_AmountOf = amount of events of this type
            //_TimeSpent = total time spent across all events of this type

            // In vc
            public long VC_AmountOf = 0;
            public long VC_TimeSpent = 0;
            public long VC_TimeDeafened = 0;
            public long VC_TimeMuted = 0;
            public long VC_TimeSpeaking = 0;

            // Streaming
            public long Streaming_AmountOf = 0;
            public long Streaming_TimeSpent = 0;
            public long Streaming_BitsSent = 0;
            public long Streaming_TotalAverageFPS = 0;

            // Watching streams
            public long WatchingStream_AmountOf = 0;
            public long WatchingStream_TimeSpent = 0;

            // Messages
            public long Messages_AmountOf = 0;
            public long Messages_AttachmentsSent = 0;
            public long Messages_EmbedsSent = 0;
            public long Messages_SpoilersSent = 0;
            public long Messages_WordsSent = 0;
            public long Messages_CharactersSent = 0;

            public void IncrementVoiceDisconnect(voice_disconnect voice_data)
            {

                long duration_ms = voice_data.duration;
                VC_AmountOf++;
                VC_TimeSpent += duration_ms;
                VC_TimeDeafened += (voice_data.duration_connected - voice_data.duration_listening) * 1000;
                VC_TimeMuted += (voice_data.duration_connected - voice_data.duration_participation) * 1000;
                VC_TimeSpeaking += (voice_data.duration_speaking) * 1000;
            }

            public void IncrementVideoStreamEnded(video_stream_ended stream_data)
            {

                switch (stream_data.participant_type)
                {

                    case ("streamer"):
                        {
                            long duration_ms = stream_data.duration * 1000;
                            Streaming_AmountOf++;
                            Streaming_TimeSpent += duration_ms;
                            Streaming_BitsSent += stream_data.avg_bitrate * (duration_ms / 1000);
                            Streaming_TotalAverageFPS += stream_data.avg_fps;
                            break;
                        }
                    case ("sender"):
                        {
                            long duration_ms = stream_data.duration * 1000;
                            Streaming_AmountOf++;
                            Streaming_TimeSpent += duration_ms;
                            Streaming_BitsSent += stream_data.avg_bitrate * (duration_ms / 1000);
                            Streaming_TotalAverageFPS += stream_data.avg_fps;
                            break;
                        }
                    case ("receiver"):
                        {
                            long duration_ms = stream_data.duration * 1000;
                            WatchingStream_AmountOf++;
                            WatchingStream_TimeSpent += duration_ms;
                            break;
                        }
                    default:
                        {
                            Console.WriteLine($"UNKNOWN STREAM CASE!: {stream_data.participant_type} #{AnonymizeGuildOrChannelId(stream_data.channel_id)}");
                            break;
                        }
                }
            }

            public void IncrementSendMessage(send_message message_data)
            {

                Messages_AmountOf++;
                Messages_AttachmentsSent += message_data.num_attachments;
                Messages_EmbedsSent += message_data.num_embeds;
                if (message_data.has_spoiler)
                {

                    Messages_SpoilersSent++;
                }
                Messages_WordsSent += message_data.word_count;
                Messages_CharactersSent += message_data.length;
            }
        }

        internal class video_stream_ended
        {

            public long duration = 0; // In seconds?

            // Both of these are always 0 when recieving a stream, but not when sending a stream, for some fucking reason
            public long avg_bitrate = 0; // in mbps?
            public long avg_fps = 0;
            //public long num_frames = 0;

            public string participant_type = "UNKNOWN"; // streamer | receiver / sender
            public long max_viewers = 0;

            public string share_application_name = "N/A";
            //public long share_application_id = "n/a";
            //public string share_application_executable = "no_game.exe";

            public Nullable<long> guild_id = null;
            public long channel_id = 0; // Has no channel_type, if guild_id is present its in a server, else in dms/groupchat
            public string timestamp = "\"1970-11-11T11:11:11.111Z\"";

            public ChannelType EnumChannelType; // Not assigned by default, assigned after the message is created
            public void UpdateEnumChannelType(string dir)
            {

                if (ChannelTypeCache.ContainsKey(channel_id))
                {

                    //Console.WriteLine($"Cache hit! #{channel_id}");
                    this.EnumChannelType = ChannelTypeCache[channel_id];
                    return;
                }

                //Console.WriteLine($"Stream channeltype cache miss for #{channel_id}");
                if (guild_id != null)
                {

                    //Console.WriteLine($"is guild {guild_id}");
                    ChannelTypeCache[channel_id] = ChannelType.GuildGeneric;
                    this.EnumChannelType = ChannelType.GuildGeneric;
                    return;
                }
                else if (max_viewers >= 2) // More than 2 people were watching the stream, this is a groupchat
                {

                    //Console.WriteLine($"is gc (maxviewer) {channel_id}");
                    ChannelTypeCache[channel_id] = ChannelType.Groupchat;
                    this.EnumChannelType = ChannelType.Groupchat;
                    return;
                }
                else // Unknown, check the message index
                {

                    string path_to_channel = $"{dir}\\messages\\c{channel_id}\\channel.json";
                    if (!Directory.Exists(path_to_channel))
                    {
                        Console.WriteLine($"Channel has no message data, cannot determine type for #{AnonymizeGuildOrChannelId(channel_id)}");
                        this.EnumChannelType = ChannelType.Other_Unhandled;
                        return;
                        //ChannelTypeCache[channel_id] = ChannelType.Other_Unknown;
                    }

                    StreamReader read = new StreamReader(path_to_channel);
                    ChannelMetaData meta = JsonConvert.DeserializeObject<ChannelMetaData>(read.ReadToEnd());

                    switch (meta.type)
                    {
                        case ("GROUP_DM"):
                            {
                                //Console.WriteLine($"fileread is gc {channel_id}");
                                ChannelTypeCache[channel_id] = ChannelType.Groupchat;
                                this.EnumChannelType = ChannelType.Groupchat;
                                return;
                            }
                        case ("DM"):
                            {
                                //Console.WriteLine($"fileread is dm {channel_id}");
                                ChannelTypeCache[channel_id] = ChannelType.UserDM;
                                this.EnumChannelType = ChannelType.UserDM;
                                return;
                            }
                        case ("GUILD_TEXT"):
                            {
                                //Console.WriteLine($"fileread is guild {channel_id}");
                                ChannelTypeCache[channel_id] = ChannelType.GuildGeneric;
                                this.EnumChannelType = ChannelType.GuildGeneric;
                                Console.WriteLine("unexpected GUILD_TEXT");
                                return;
                            }
                        default:
                            {
                                Console.WriteLine($"Unhandled channel type: {meta.type} for #{AnonymizeGuildOrChannelId(channel_id)} @{AnonymizeGuildOrChannelId(guild_id)}");
                                if (meta.type.Contains("GUILD"))
                                {

                                    this.EnumChannelType = ChannelType.Other_GuildUnknown;
                                }
                                else
                                {

                                    //this.EnumChannelType = ChannelType.Other_Unhandled;
                                }
                                return;
                            }
                    }
                }
            }
        }

        internal class voice_disconnect
        {

            //TODO deal with these packets changing ~2024? to have duration_type_ms instead of only duration_type

            public long duration = 0; // In ms? i think this also includes time spent connecting?

            public long duration_listening = 0; // Time spent undeafened
            public long duration_participation = 0; // Time spent unmuted?
            public long duration_speaking = 0; // Time spent transmitting audio
            public long duration_connected = 0; // Time spent connected to the vc

            public Nullable<long> guild_id = null;
            public long channel_id = 0;
            public long channel_type = 0; // 0 = unknown (user dms?), 2 = server voice channel (has guild_id), 3 = groupchat
            public string timestamp = "\"1970-11-11T11:11:11.111Z\"";

            public ChannelType EnumChannelType; // Not assigned by default, assigned after the message is created
            public void UpdateEnumChannelType(string dir)
            {

                switch (channel_type)
                {
                    case (0):
                        {
                            ChannelTypeCache[channel_id] = ChannelType.Other_GuildUnknown;
                            this.EnumChannelType = ChannelType.Other_GuildUnknown;
                            //Console.WriteLine($"voice_disconnect.channel_type = 0   {channel_id} {guild_id}");
                            break;
                        }
                    case (1):
                        {
                            ChannelTypeCache[channel_id] = ChannelType.UserDM;
                            this.EnumChannelType = ChannelType.UserDM;
                            //Console.WriteLine($"voice_disconnect.channel_type = 1   {channel_id} {guild_id}");
                            break;
                        }
                    case (2):
                        {
                            ChannelTypeCache[channel_id] = ChannelType.GuildGeneric;
                            this.EnumChannelType = ChannelType.GuildGeneric;
                            break;
                        }
                    case (3):
                        {
                            ChannelTypeCache[channel_id] = ChannelType.Groupchat;
                            this.EnumChannelType = ChannelType.Groupchat;
                            break;
                        }
                    default:
                        {
                            if (this.guild_id != null)
                            {

                                ChannelTypeCache[channel_id] = ChannelType.Other_GuildUnknown;
                                this.EnumChannelType = ChannelType.Other_GuildUnknown;
                            } else
                            {

                                ChannelTypeCache[channel_id] = ChannelType.Other_Unhandled;
                                //this.EnumChannelType = ChannelType.Other_Unhandled;
                            }
                            //Console.WriteLine($"UNHANDLED voice_disconnect.channel_type = {channel_type}   {channel_id} {guild_id}");
                            break;
                        }
                }
            }
        }

        internal class send_message
        {

            // Message metadata shit
            public long num_attachments = 0;
            public long num_embeds = 0;
            public Boolean emoji_only = false;
            public Boolean has_spoiler = false;
            public Boolean is_friend = false;

            public long word_count = 0;
            public long length = 0;

            public long message_type = 0; // ??????

            // Channel metadata stuff
            public Nullable<long> server = null; // guild_id
            public long channel = 0; // channel_id
            public long channel_type = 0; // 0 = server text channel, 
            public string timestamp = "\"1970-11-11T11:11:11.111Z\"";

            public ChannelType EnumChannelType; // Not assigned by default, assigned after the message is created
            public void UpdateEnumChannelType(string dir)
            {

                switch (channel_type)
                {
                    case (0):
                        {
                            ChannelTypeCache[channel] = ChannelType.GuildGeneric;
                            this.EnumChannelType = ChannelType.GuildGeneric;
                            break;
                        }
                    case (1):
                        {
                            ChannelTypeCache[channel] = ChannelType.UserDM;
                            this.EnumChannelType = ChannelType.UserDM;
                            break;
                        }
                    case (3):
                        {
                            ChannelTypeCache[channel] = ChannelType.Groupchat;
                            this.EnumChannelType = ChannelType.Groupchat;
                            break;
                        }
                    case (10 | 11 | 12 | 13 | 2 |5):
                        {
                            ChannelTypeCache[channel] = ChannelType.Other_GuildUnknown;
                            this.EnumChannelType = ChannelType.Other_GuildUnknown;
                            //Console.WriteLine($"send_message.channel_type = {channel_type}   {channel} {server}");
                            break;
                        }
                    default:
                        {
                            if (this.server != null)
                            {

                                ChannelTypeCache[channel] = ChannelType.Other_GuildUnknown;
                                this.EnumChannelType = ChannelType.Other_GuildUnknown;
                            } else
                            {

                                Console.WriteLine($"UNHANDLED non guild send_message.channel_type = {channel_type}   {AnonymizeGuildOrChannelId(channel)} {AnonymizeGuildOrChannelId(server)}");
                                ChannelTypeCache[channel] = ChannelType.Other_Unhandled;
                               // this.EnumChannelType = ChannelType.Other_Unhandled;
                            }
                            //Console.WriteLine($"UNHANDLED voice_disconnect.channel_type = {channel_type}   {channel} {server}");
                            break;
                        }
                }
            }
        }

        static string MillisecondsToText(long ms)
        {

            TimeSpan t = TimeSpan.FromMilliseconds(ms);
            return string.Format("{0:D2}h:{1:D2}m:{2:D2}s:{3:D3}ms",
                t.Hours + (t.Days * 24),
                t.Minutes,
                t.Seconds,
                t.Milliseconds);
        }

        static string MillisecondsToTextShorter(long ms)
        {

            TimeSpan t = TimeSpan.FromMilliseconds(ms);
            return string.Format("{0:D2}h:{1:D2}m:",
                t.Hours + (t.Days * 24),
                t.Minutes);
        }

        static string BitsToText(long bit)
        {

            long bytes = bit / 8;
            if (bytes < 1024) // under a kb, use b
            {

                return $"{bytes}B";
            }
            else if (bytes < 1048576) // Under a mb, use kb
            {

                double burger = (float)bytes;
                return $"{(burger / 1000).ToString("0.00")}KB ({(burger / 1024).ToString("0.00")}KiB)";
            }
            else if (bytes < 1073742000) // Under a gb, use mb
            {

                double burger = (float)bytes;
                return $"{(burger / 1000000).ToString("0.00")}MB ({(burger / 1048576).ToString("0.00")}MiB)";
            }
            else if (bytes < 1099512000000) // Under a tb, use gb
            {

                double burger = (float)bytes;
                return $"{(burger / 1000000000).ToString("0.00")}GB ({(burger / 1073742000).ToString("0.00")}GiB)";
            }
            else // over a tb, use tb
            {

                double burger = (float)bytes;
                return $"{(burger / 1000000000000).ToString("0.00")}TB  ({(burger / 1099512000000).ToString("0.00")}TiB)";
            }
        }

        static void Main()
        {

            Console.Title = $"Discord Data Summarizer VC2";

            // Get directory info from user
            Console.WriteLine("Please enter directory/path of unzipped discord data packet\nExample: C:\\Downloads\\package\nJust copy paste it or something");
            string read_path = @"" + Console.ReadLine();
            if (!Directory.Exists(read_path))
            {

                Console.WriteLine("FAIL: Path does not exist\nPress enter to exit.");
                Console.ReadLine(); // Wait for input
                throw new Exception("Path doesnt exist");
            }
            else if (!File.Exists(read_path + "\\messages\\index.json")) // Ensure path has messages and index.json
            {

                Console.WriteLine("FAIL: Path does not contain 'messages\\index.json', this may not be a data packet, or it may be missing messages\npress enter to exit.");
                Console.ReadLine(); // Wait for input
                throw new Exception("Path not data packet");
            }

            DoStuff(read_path);
        }

        static void DoStuff(string dir)
        {

            ChannelData ChannelData_Global = new ChannelData();
            ChannelData ChannelData_Servers = new ChannelData();
            ChannelData ChannelData_Groupchats = new ChannelData();
            ChannelData ChannelData_Users = new ChannelData(); // Presumably user dms?
            ChannelData ChannelData_Other = new ChannelData(); // unknowns

            SortedDictionary<long, ChannelData> ChannelDataPerChannelMap = new();
            SortedDictionary<long, SortedDictionary<long, ChannelData>> ChannelDataPerChannelPerYearMap = new();
            SortedDictionary<long, ChannelData> ChannelDataPerYearMap = new();

            // Returns any of the category channeldata instances, e.g. ChannelData_Global or ChannelData_Servers
            ChannelData GetCategoryChannelDataToIncrement(ChannelType channel_type)
            {

                switch (channel_type)
                {

                    case (ChannelType.GuildGeneric):
                        {
                            return ChannelData_Servers;
                        }
                    case (ChannelType.Other_GuildUnknown):
                        {
                            return ChannelData_Servers;
                        }
                    case (ChannelType.Groupchat):
                        {
                            return ChannelData_Groupchats;
                        }
                    case (ChannelType.UserDM):
                        {
                            return ChannelData_Users;
                        }
                    default:
                        {
                            return ChannelData_Other;
                        }
                }
            };

            // Returns ChannelDataPerChannelPerYearMap[year][channel_id] with handling for if the index is null so it doesnt error
            ChannelData GetChannelDataForYearAndChannelId(long year, long channel_id)
            {

                if (!ChannelDataPerChannelPerYearMap.ContainsKey(year))
                {

                    ChannelDataPerChannelPerYearMap[year] = new();
                }

                SortedDictionary<long, ChannelData> channel_data_map = ChannelDataPerChannelPerYearMap[year];
                if (!channel_data_map.ContainsKey(channel_id))
                {

                    channel_data_map[channel_id] = new ChannelData();
                }

                return channel_data_map[channel_id];
            }

            ChannelData GetChannelDataForYear(long year)
            {

                if (!ChannelDataPerYearMap.ContainsKey(year))
                {

                    ChannelDataPerYearMap[year] = new();
                }

                return ChannelDataPerYearMap[year];
            }

            ChannelData GetChannelDataForChannelId(long channel_id)
            {

                if (!ChannelDataPerChannelMap.ContainsKey(channel_id))
                {

                    ChannelDataPerChannelMap[channel_id] = new();
                }

                return ChannelDataPerChannelMap[channel_id];
            }

            DateTime TimestampToDatetime(string timestamp)
            {

                return DateTime.Parse(timestamp.Replace("\"", ""));
            }

            // Channel name data
            SortedDictionary<long, string> MetaData_ChannelNameIndex = JsonConvert.DeserializeObject<SortedDictionary<long, string>>(new StreamReader(dir + "\\messages\\index.json").ReadToEnd());
            SortedDictionary<long, string> MetaData_GuildNameIndex = JsonConvert.DeserializeObject<SortedDictionary<long, string>>(new StreamReader(dir + "\\Servers\\index.json").ReadToEnd());

            // Classification stuff
            SortedDictionary<string, long> Classification_Map = new SortedDictionary<string, long>();
            void IncrementClassification(string event_type)
            {

                if (!Classification_Map.ContainsKey(event_type))
                {

                    Classification_Map[event_type] = 1;
                } else
                {

                    Classification_Map[event_type]++;
                }
            }


            StreamReader shitr = new StreamReader(dir + "\\Activity\\analytics\\events-2025-00000-of-00001.json");

            // Read every line so we can know what the line count is
            Int64 whatlineareweon = 0;
            Int64 linecount = 0;
            using (StreamReader line_checker = new StreamReader(dir + "\\Activity\\analytics\\events-2025-00000-of-00001.json"))
            {

                while (!line_checker.EndOfStream)
                {

                    if (linecount % 90000 == 0)
                    {

                        Console.Clear();
                        Console.Write("Calculating file size: " + linecount + " lines\n");
                    }

                    line_checker.ReadLine();
                    linecount++;
                }
            }

            Console.WriteLine("Processing stuff, this may take a while");

            while (true)
            {

                string line_data = shitr.ReadLine();
                //Console.WriteLine("line " + line_data);
                if (line_data == null || whatlineareweon > CUTOFF_FILE_LINE)
                {

                    Console.WriteLine("Null linedata, breaking");
                    break;
                }

                whatlineareweon++;
                if (whatlineareweon % 200000 == 0)
                {

                    Console.WriteLine($"Progess: {(((float)whatlineareweon / (float)linecount) * 100).ToString("0.0")}% ({whatlineareweon}/{linecount} lines parsed)");
                }

                classification event_classification = JsonConvert.DeserializeObject<classification>(line_data);
                //IncrementClassification(event_classification.event_type);

                #region
                if ((event_classification.guild_id != null) & (event_classification.channel_id != null))
                {

                    long guild_id = (long)event_classification.guild_id;
                    long channel_id = (long)event_classification.channel_id;
                    if (!MetaData_ChannelIdToGuildIdMap.ContainsKey(channel_id))
                    {

                        MetaData_ChannelIdToGuildIdMap[channel_id] = guild_id;
                    } else
                    {
                        // Incase it defaults to 0
                        MetaData_ChannelIdToGuildIdMap[channel_id] = Math.Max(guild_id, MetaData_ChannelIdToGuildIdMap[channel_id]);
                    }
                }
                #endregion
                switch (event_classification.event_type)
                {
                    case ("voice_disconnect"):
                        {

                            voice_disconnect this_event = JsonConvert.DeserializeObject<voice_disconnect>(line_data);

                            long year = TimestampToDatetime(this_event.timestamp).Year;
                            if (year < CUTOFF_YEAR)
                            {

                                continue;
                            }

                            this_event.UpdateEnumChannelType(dir);
                            ChannelData category_data = GetCategoryChannelDataToIncrement(this_event.EnumChannelType);
                            ChannelData per_data = GetChannelDataForYearAndChannelId(year, this_event.channel_id);
                            ChannelData per_year = GetChannelDataForYear(year);
                            ChannelData per_channel = GetChannelDataForChannelId(this_event.channel_id);

                            per_channel.IncrementVoiceDisconnect(this_event);
                            per_year.IncrementVoiceDisconnect(this_event);
                            category_data.IncrementVoiceDisconnect(this_event);
                            per_data.IncrementVoiceDisconnect(this_event);
                            ChannelData_Global.IncrementVoiceDisconnect(this_event);

                            //IncrementClassification($"voice_disconnect.channel_type {this_event.channel_type}");

                            break;
                        }
                    case ("video_stream_ended"):
                        {

                            video_stream_ended this_event = JsonConvert.DeserializeObject<video_stream_ended>(line_data);

                            long year = TimestampToDatetime(this_event.timestamp).Year;
                            if (year < CUTOFF_YEAR)
                            {

                                continue;
                            }

                            this_event.UpdateEnumChannelType(dir);
                            ChannelData category_data = GetCategoryChannelDataToIncrement(this_event.EnumChannelType);
                            ChannelData per_data = GetChannelDataForYearAndChannelId(year, this_event.channel_id);
                            ChannelData per_year = GetChannelDataForYear(year);
                            ChannelData per_channel = GetChannelDataForChannelId(this_event.channel_id);

                            per_channel.IncrementVideoStreamEnded(this_event);
                            per_year.IncrementVideoStreamEnded(this_event);
                            category_data.IncrementVideoStreamEnded(this_event);
                            per_data.IncrementVideoStreamEnded(this_event);
                            ChannelData_Global.IncrementVideoStreamEnded(this_event);

                            break;
                        }
                    case ("send_message"):
                        {

                            send_message this_event = JsonConvert.DeserializeObject<send_message>(line_data);

                            long year = TimestampToDatetime(this_event.timestamp).Year;
                            if (year < CUTOFF_YEAR)
                            {

                                continue;
                            }

                            this_event.UpdateEnumChannelType(dir);
                            ChannelData category_data = GetCategoryChannelDataToIncrement(this_event.EnumChannelType);
                            ChannelData per_data = GetChannelDataForYearAndChannelId(year, this_event.channel);
                            ChannelData per_year = GetChannelDataForYear(year);
                            ChannelData per_channel = GetChannelDataForChannelId(this_event.channel);

                            per_channel.IncrementSendMessage(this_event);
                            per_year.IncrementSendMessage(this_event);
                            category_data.IncrementSendMessage(this_event);
                            per_data.IncrementSendMessage(this_event);
                            ChannelData_Global.IncrementSendMessage(this_event);

                            break;
                        }
                }
            }

            string ChannelIdToName(long channel_id)
            {

                if (DO_ANONYMIZE_GUIDS)
                {

                    return "[Name hidden]";
                }

                if (MetaData_ChannelNameIndex.ContainsKey(channel_id))
                {

                    return MetaData_ChannelNameIndex[channel_id];
                }

                if (MetaData_ChannelIdToGuildIdMap.ContainsKey(channel_id))
                {

                    long guild_id = MetaData_ChannelIdToGuildIdMap[channel_id];
                    if (MetaData_GuildNameIndex.ContainsKey(guild_id))
                    {

                        return $"[!] <UNKNOWN CHANNEL [{channel_id}]> in {MetaData_GuildNameIndex[guild_id]}";
                    } else
                    {

                        return $"[!!] <UNKNOWN CHANNEL[{channel_id}]> in <UNKNOWN GUILD [{guild_id}]>";
                    }
                }

                return $"[!!!] <UNKNOWN CHANNEL [{channel_id}]>";
            }

            static void WriteLineSplitDrk(string str)
            {

                string[] split = str.Split("$@SPLITHERE$@");

                Console.Write(split[0]);
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"   |   {split[1]}");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("\n");
            }

            static void WriteLineRed(string str)
            {

                //ConsoleColor prevback = Console.BackgroundColor;
                ConsoleColor prevfore = Console.ForegroundColor;
                //Console.BackgroundColor = ConsoleColor.DarkRed;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(str);
                //Console.BackgroundColor = prevback;
                Console.ForegroundColor = prevfore;
                Console.Write("\n");
            }

            static void WriteLineDarkGray(string str)
            {

                //ConsoleColor prevback = Console.BackgroundColor;
                ConsoleColor prevfore = Console.ForegroundColor;
                //Console.BackgroundColor = ConsoleColor.DarkRed;
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(str);
                //Console.BackgroundColor = prevback;
                Console.ForegroundColor = prevfore;
                Console.Write("\n");
            }

            void DisplayChannelData(ChannelData data, string identifier)
            {

                //string channel_name = ChannelIdToName(channel_id);
                long amount_of_vcs = data.VC_AmountOf;
                long amount_of_streams_watched = data.WatchingStream_AmountOf;
                long amount_of_streams_made = data.Streaming_AmountOf;
                long amount_of_messages = data.Messages_AmountOf;
                WriteLineDarkGray("-=-=-=-=-=-=--=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-");
                Console.WriteLine($"{identifier}\n");

                // Totals
                #region
                //Console.WriteLine($"  [Totals]");
                if (amount_of_messages > 0)
                {

                    Console.WriteLine($"  [Messages]");
                    Console.WriteLine($"    Messages sent:        {amount_of_messages}");
                    WriteLineSplitDrk($"    Attachments sent:     {data.Messages_AttachmentsSent}$@SPLITHERE$@ {((float)data.Messages_AttachmentsSent / amount_of_messages).ToString("0.000")} average attachments per message");
                    WriteLineSplitDrk($"    Embeds sent:          {data.Messages_EmbedsSent}$@SPLITHERE$@ {((float)data.Messages_EmbedsSent / amount_of_messages).ToString("0.000")} average embeds per message");
                    WriteLineSplitDrk($"    Spoilers sent:        {data.Messages_SpoilersSent}$@SPLITHERE$@ {(((float)data.Messages_SpoilersSent / amount_of_messages) * 100).ToString("0.000")}% of all messages that are spoilered");
                    WriteLineSplitDrk($"    Words sent:           {data.Messages_WordsSent}$@SPLITHERE$@ {((float)data.Messages_WordsSent / amount_of_messages).ToString("0.000")} average words per message");
                    WriteLineSplitDrk($"    Characters sent:      {data.Messages_CharactersSent}$@SPLITHERE$@ {((float)data.Messages_CharactersSent / amount_of_messages).ToString("0.000")} average characters per message");
                    Console.WriteLine("");
                }
                else
                {

                    WriteLineRed($"  [Messages] <-- NO DATA");
                }

                if (amount_of_vcs > 0)
                {

                    Console.WriteLine($"  [Voice]");
                    Console.WriteLine($"    VCs joined:           {amount_of_vcs}");
                    WriteLineSplitDrk($"    Time spent in VC:     {MillisecondsToText(data.VC_TimeSpent)}$@SPLITHERE$@ {MillisecondsToText(data.VC_TimeSpent / amount_of_vcs)} average time in each vc session");
                    //WriteLineSplitDrk($"    Time spent muted:     {MillisecondsToText(data.VC_TimeMuted)}$@SPLITHERE$@ average {MillisecondsToText(data.VC_TimeMuted / amount_of_vcs)} spent muted per vc");
                    //WriteLineSplitDrk($"    Time spent deafened:  {MillisecondsToText(data.VC_TimeDeafened)}$@SPLITHERE$@ average {MillisecondsToText(data.VC_TimeDeafened / amount_of_vcs)} spent deafend per vc");
                    WriteLineSplitDrk($"    Time spent speaking:  {MillisecondsToText(data.VC_TimeSpeaking)}$@SPLITHERE$@ {MillisecondsToText(data.VC_TimeSpeaking / amount_of_vcs)} average time spent speaking in each vc session");
                    //WriteLineRed("Note: Deafened + muted times are most likely completely incorrect");
                    Console.WriteLine("");
                }
                else
                {

                    WriteLineRed($"  [Voice] <-- NO DATA");
                }

                if (amount_of_streams_made > 0)
                {

                    Console.WriteLine($"  [Streaming]");
                    Console.WriteLine($"    Streams started:      {amount_of_streams_made}");
                    WriteLineSplitDrk($"    Time spent streaming: {MillisecondsToText(data.Streaming_TimeSpent)}$@SPLITHERE$@ {MillisecondsToText(data.Streaming_TimeSpent / Math.Max(1, amount_of_vcs))} average time spent streaming per vc");
                    WriteLineSplitDrk($"    Data streamed:        {BitsToText(data.Streaming_BitsSent)}$@SPLITHERE$@ {BitsToText(data.Streaming_BitsSent / Math.Max(1, amount_of_vcs))} average data sent per stream");
                    Console.WriteLine($"    Average fps:          {((float)data.Streaming_TotalAverageFPS / amount_of_streams_made).ToString("0.00")}");
                    Console.WriteLine("");
                }
                else
                {

                    WriteLineRed($"  [Streaming] <-- NO DATA");
                }

                if (amount_of_streams_watched > 0)
                {

                    Console.WriteLine($"  [Watching streams]");
                    Console.WriteLine($"    Streams watched:      {amount_of_streams_watched}");
                    WriteLineSplitDrk($"    Time spent watching:  {MillisecondsToText(data.WatchingStream_TimeSpent)}$@SPLITHERE$@ {MillisecondsToText(data.WatchingStream_TimeSpent / Math.Max(1, amount_of_vcs))} average time spent watching streams each vc");
                }
                else
                {

                    WriteLineRed($"  [Watching streams] <-- NO DATA");
                }
                #endregion

                Console.WriteLine("");
            }

            void DisplayChannelDataShort(ChannelData data)
            {

                long amount_of_vcs = data.VC_AmountOf;
                long amount_of_streams_made = data.Streaming_AmountOf;
                long amount_of_messages = data.Messages_AmountOf;

                if (amount_of_messages > 0)
                {

                    WriteLineSplitDrk($"            Messages sent:    {amount_of_messages}$@SPLITHERE$@ Attachments: {data.Messages_AttachmentsSent} | Embeds: {data.Messages_EmbedsSent}");
                    //WriteLineSplitDrk($"    Spoilers sent:        {data.Messages_SpoilersSent}$@SPLITHERE$@ Words: {data.Messages_WordsSent} | Characters: {data.Messages_CharactersSent}");
                }
                else
                {

                    //WriteLineRed($"     You never sent a message");
                }

                if (amount_of_vcs > 0)
                {

                    WriteLineSplitDrk($"            VCs joined:       {amount_of_vcs}$@SPLITHERE$@ Time spent in vcs: {MillisecondsToTextShorter(data.VC_TimeSpent)}");
                    //WriteLineSplitDrk($"      Time muted:       {MillisecondsToTextShorter(data.VC_TimeMuted)}$@SPLITHERE$@ Time deafened: {MillisecondsToTextShorter(data.VC_TimeDeafened)}");
                }
                else
                {

                    //WriteLineRed($"        You never joined a voice chat");
                }

                if (amount_of_streams_made > 0)
                {

                    WriteLineSplitDrk($"            Streams started:  {amount_of_streams_made}$@SPLITHERE$@ Time spent streaming: {MillisecondsToTextShorter(data.Streaming_TimeSpent)} | Data sent: {BitsToText(data.Streaming_BitsSent)}");
                }
                else
                {

                    //WriteLineRed($"     You never streamed");
                }

                Console.WriteLine("");
            }

            void DisplayBestChannels(KeyValuePair<long, ChannelData>[] arr, int amount_to_show)
            {

                int how_many_lowers_done = 0;
                foreach (KeyValuePair<long, ChannelData> lower_pair in arr)
                {

                    how_many_lowers_done++;
                    if (how_many_lowers_done > amount_to_show)
                    {

                        break;
                    }

                    //DisplayChannelDataShort(lower_pair.Value, $"Short data for {AnonymizeGuildOrChannelId(lower_pair.Key)}");
                    Console.WriteLine($"          #{how_many_lowers_done}: {ChannelIdToName(lower_pair.Key)}");
                    DisplayChannelDataShort(lower_pair.Value);
                }
            }

            Console.WriteLine("\n\n\n\n\n\n\n\n\n\n\n\n");

            DisplayChannelData(ChannelData_Global, $"Lifetime data (Global)");
            DisplayChannelData(ChannelData_Servers, $"Lifetime data (Servers)");
            DisplayChannelData(ChannelData_Groupchats, $"Lifetime data (Groupchats)");
            DisplayChannelData(ChannelData_Users, $"Lifetime data (Dms)");
            DisplayChannelData(ChannelData_Other, $"Lifetime data (OTHER/UNKNOWN)");

            WriteLineDarkGray("____________________________________________________________");
            WriteLineSplitDrk($"        [Top 10 lifetime channels]$@SPLITHERE$@sorted by most messages");
            DisplayBestChannels(ChannelDataPerChannelMap.ToArray().OrderByDescending(d => d.Value.Messages_AmountOf).ToArray(), 10);

            WriteLineDarkGray("____________________________________________________________");
            WriteLineSplitDrk($"        [Top 10 lifetime channels]$@SPLITHERE$@sorted by most voice chat time");
            DisplayBestChannels(ChannelDataPerChannelMap.ToArray().OrderByDescending(d => d.Value.VC_TimeSpent).ToArray(), 10);

            Console.WriteLine("\n\n\n\n----------------------------------------------------------------------");
            Console.WriteLine("Yearly data");
            Console.WriteLine("----------------------------------------------------------------------\n");

            foreach (KeyValuePair<long, ChannelData> pair in ChannelDataPerYearMap.ToArray().OrderByDescending(d => d.Key).ToArray())
            {

                Console.WriteLine("\n\n");
                DisplayChannelData(pair.Value, $"Data for year {pair.Key} (Global)");

                WriteLineDarkGray("____________________________________________________________");
                WriteLineSplitDrk($"        [Top 3 channels for year {pair.Key}]$@SPLITHERE$@sorted by most messages");
                DisplayBestChannels(ChannelDataPerChannelPerYearMap[pair.Key].ToArray().OrderByDescending(d => d.Value.Messages_AmountOf).ToArray(), 3);

                WriteLineDarkGray("____________________________________________________________");
                WriteLineSplitDrk($"       [Top 3 channels for year {pair.Key}]$@SPLITHERE$@sorted by most voice chat time");
                DisplayBestChannels(ChannelDataPerChannelPerYearMap[pair.Key].ToArray().OrderByDescending(d => d.Value.VC_TimeSpent).ToArray(), 3);
            }

            // Dump classification log
            if (Classification_Map.Count > 0)
            {

                Console.WriteLine("Classification dump follows:");
                foreach (KeyValuePair<string, long> pair in Classification_Map.ToArray())
                {

                    Console.WriteLine($"{pair.Key}: {pair.Value}");
                }
            }

            //Console.Beep();
            Console.WriteLine("Finished, press enter to exit, scroll up to see summarized data");
            Console.ReadLine(); // Wait for user input, once program ends itll close probably
        }
    }
}
