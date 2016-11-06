﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Xml.Linq;
using LightPi.Orchestrator;
using NAudio.Midi;

namespace LightPi.Midi2OrchestratorBridge
{
    internal static class Program
    {
        private static readonly Dictionary<string, int> _mappings = new Dictionary<string, int>();

        private static MidiIn _midiIn;
        private static OrchestratorClient _orchestratorClient;
        private static XDocument _settings;

        public static void Main()
        {
            Console.Title = "MIDI 2 Orchestrator Bridge - LightPi";
            WriteBanner();

            try
            {
                LoadSettings();
                InitializeOrchestratorClient();
                LoadMappings();
                InitializeMidiInput();

                WriteOutput(ConsoleColor.Gray, string.Empty);
                WriteOutput(ConsoleColor.Green, "Waiting for MIDI events (Press <Enter> key to exit)...");
                Console.ReadLine();
            }
            catch (Exception exception)
            {
                WriteOutput(ConsoleColor.Red, ConsoleColor.White, $"{Environment.NewLine}An error has occurred:");
                WriteOutput(ConsoleColor.Red, exception.ToString());
                WriteOutput(ConsoleColor.Green, "Press <Enter> key to exit");
                Console.ReadLine();
            }
        }

        private static void LoadSettings()
        {
            _settings = XDocument.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LightPi.Midi2OrchestratorBridgeSettings.xml"));
        }

        private static void WriteBanner()
        {
            WriteOutput(ConsoleColor.Red, ConsoleColor.White, "                                                              ");
            WriteOutput(ConsoleColor.Red, ConsoleColor.White, "  +--------------------------------------------------------+  ");
            WriteOutput(ConsoleColor.Red, ConsoleColor.White, "  |                                                        |  ");
            WriteOutput(ConsoleColor.Red, ConsoleColor.White, "  |          MIDI 2 Orchestrator Bridge - LightPi          |  ");
            WriteOutput(ConsoleColor.Red, ConsoleColor.White, "  |          ------------------------------------          |  ");
            WriteOutput(ConsoleColor.Red, ConsoleColor.White, "  |       (c) Christian Kratky 2016 (www.ha4iot.de)        |  ");
            WriteOutput(ConsoleColor.Red, ConsoleColor.White, "  |      Using 'naudio' library (naudio.codeplex.com)      |  ");
            WriteOutput(ConsoleColor.Red, ConsoleColor.White, "  |                                                        |  ");
            WriteOutput(ConsoleColor.Red, ConsoleColor.White, "  +--------------------------------------------------------+  ");
            WriteOutput(ConsoleColor.Red, ConsoleColor.White, "                                                              ");
            WriteOutput(ConsoleColor.Red, ConsoleColor.White, string.Empty);
        }

        private static void InitializeMidiInput()
        {
            string midiDevideFromSettings = _settings.Root.Element("MidiIn").Value;
            
            WriteOutput(ConsoleColor.Magenta, "[ MIDI devices ]");

            int deviceId = 0;
            int m = MidiIn.NumberOfDevices;
            for (int i = 0; i < m; i++)
            {
                var devicInfo = MidiIn.DeviceInfo(i);
                
                if (devicInfo.ProductName == midiDevideFromSettings)
                {
                    deviceId = i;
                    WriteOutput(ConsoleColor.Green, $"\t{i}: [{devicInfo.ProductName}] [Used]");
                }
                else
                {
                    WriteOutput(ConsoleColor.Gray, $"\t{i}: [{devicInfo.ProductName}]");
                }
            }
            
            _midiIn = new MidiIn(deviceId);
            _midiIn.MessageReceived += ProcessMidiMessage;
            _midiIn.ErrorReceived += ProcessMidiError;
            _midiIn.Start();
        }

        private static void InitializeOrchestratorClient()
        {
            var ipAddress = IPAddress.Parse(_settings.Root.Element("OrchestratorAddress").Value);

            // TODO: Support hostname by resolving using Dns.GetHost...

            WriteOutput(ConsoleColor.Magenta, "[ Orchestrator ]");
            WriteOutput(ConsoleColor.Gray, $"\tAddress: {ipAddress}");

            _orchestratorClient = new OrchestratorClient(ipAddress);
            _orchestratorClient.CommitChanges();
        }

        private static void LoadMappings()
        {
            var mappings = _settings.Root.Element("Mappings").Elements("Mapping");

            WriteOutput(ConsoleColor.Magenta, "[ Output mappings ]");
            foreach (var mapping in mappings)
            {
                var note = mapping.Attribute("Note").Value;
                var outputId = int.Parse(mapping.Attribute("Output").Value);
                var channelId = int.Parse(mapping.Attribute("Channel").Value);
                var comment = mapping.Attribute("Comment").Value;

                var key = GenerateMappingKey(note, channelId);
                _mappings[key] = outputId;

                string message = $"\tMapped note [{note}] from channel [{channelId}] to output [{outputId}]";
                if (!string.IsNullOrEmpty(comment))
                {
                    message += $" ({comment})";
                }

                WriteOutput(ConsoleColor.Gray, message);
            }
        }

        private static string GenerateMappingKey(string note, int channel)
        {
            return note + "@" + channel;
        }

        private static void ProcessMidiMessage(object sender, MidiInMessageEventArgs e)
        {
            var noteEvent = e.MidiEvent as NoteEvent;
            if (noteEvent == null)
            {
                WriteOutput(ConsoleColor.Red, ">> Received unsupported MIDI event: " + e.MidiEvent);
                return;
            }

            WriteOutput(ConsoleColor.White, $">> Received MIDI event: Channel-{noteEvent.Channel} / {noteEvent.NoteName} / {noteEvent.CommandCode}");

            var mappingKey = GenerateMappingKey(noteEvent.NoteName, noteEvent.Channel);
            int bit;
            if (_mappings.TryGetValue(mappingKey, out bit))
            {
                UpdateOrchestratorState(bit, noteEvent.CommandCode == MidiCommandCode.NoteOn);
            }           
        }

        private static void UpdateOrchestratorState(int bit, bool state)
        {
            _orchestratorClient.SetOutput(bit, state);
            var sendStateResult = _orchestratorClient.CommitChanges();

            var color = state ? ConsoleColor.Green : ConsoleColor.DarkGreen;
            var stateText = state ? "On" : "Off";
            WriteOutput(color, $"\t<< Set output [{bit}] to [{stateText}]");
            WriteOutput(ConsoleColor.Gray, $"\t<< Sent [{BitConverter.ToString(sendStateResult.State)}] to orchestrator within {sendStateResult.Duration.TotalMilliseconds} MS");
        }

        private static void ProcessMidiError(object sender, MidiInMessageEventArgs e)
        {
            WriteOutput(ConsoleColor.Red, "Error event: " + e.MidiEvent);
        }

        private static void WriteOutput(ConsoleColor color, string text)
        {
            Console.ForegroundColor = color;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.WriteLine(text);
        }

        private static void WriteOutput(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string text)
        {
            Console.ForegroundColor = foregroundColor;
            Console.BackgroundColor = backgroundColor;
            Console.WriteLine(text);
        }
    }
}
