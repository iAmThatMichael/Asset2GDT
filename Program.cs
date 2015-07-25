// ==========================================================
// Coding projects
// 
// Component: Call of Duty
// Purpose: Generate a GDT from weaponfiles
//
// Initial author: DidUknowiPwn
// Started: 2015-07-24
// ==========================================================

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;

namespace Weap2GDT
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
                WriteConsole("ERROR: No weapon file supplied. (Drag-and-Drop supported or use parameters.)", true);
            else
                StartExport(args);
        }
        private static void StartExport(string[] weapons)
        {
            // go through each element
            foreach(string weapon in weapons)
                ExportAsGDT(weapon);
        }
        private static void ExportAsGDT(string weapon)
        {
            // 1.0.3 - Error out if we're exporting as a folder.
            FileAttributes attr = File.GetAttributes(weapon);
            bool isFolder = (attr & FileAttributes.Directory) == FileAttributes.Directory;
            if(isFolder)
                WriteConsole("ERROR: Folder support has not been added yet!", true);
            // get the weapon name
            string weaponName = Path.GetFileNameWithoutExtension(weapon);
            // get the directory before the file
            DirectoryInfo saveFolder = Directory.GetParent(weapon);
            // generate the save location
            string saveFileLoc = saveFolder + @"\" + "weapsettings_" + weaponName + ".gdt";
            // read the file in
            string contents = File.ReadAllText(weapon);
            // split at every '\'
            string[] splitContents = contents.Split('\\');
            // 1.0.2 - make sure we're doing a weaponfile
            if(splitContents[0] != "WEAPONFILE")
                WriteConsole("ERROR: Supplied file isn't a WEAPONFLE!", true);
            // 1.0.2 - get the file type (i.e. WEAPONFILE, FLAMETABLEFILE)
            string configstringFileType = splitContents[0];
            string configstringGDFType = "";
            // sorted dictionary to auto sort the data
            SortedDictionary<string, string> d_wf = new SortedDictionary<string, string>();
            // go through a loop that incremenets at 2 to go to our new property
            for (int i = 1; i < splitContents.Length; i += 2)
            {
                // get property name
                var propName = splitContents[i];
                // get property name's value (which is always 1 above)
                var value = splitContents[i + 1];
                // some properties need fixing
                if (propName == "notetrackSoundMap")
                    value = ConcactNotetrack(value);
                else if (propName.Contains("Effect") && !String.IsNullOrEmpty(value))
                    value = FixEffectsPath(value);
                // 1.0.2 - set some GDF related properties in here
                else if (propName == "weaponClass")
                    configstringGDFType = DetermineGDFType(value);
                // 1.0.2 - remove the key if it exists already should stop the exception below.
                if (d_wf.ContainsKey(propName))
                    d_wf.Remove(propName);
                // 1.0.1 - try/catch instead of straight calling in order to catch the error.
                try
                {
                    d_wf.Add(propName, value);
                }
                catch(Exception exception)
                {
                    string output = string.Format("ERROR: Failed to add key {0} value {1}.\nReason: \"{2}\"", propName, value, exception.Message);
                    WriteConsole(output, true);
                }
            }
            // this is added in AssMan
            // 1.0.2 - moved to scoped string
            d_wf.Add("configstringFileType", configstringFileType);
            // also added in AssMan, check if our weaponfile is MP/SP and name it accordingly
            // 1.0.2 - moved to ternary
            string targetFolder = (!weaponName.Contains("_mp")) ? "1: Single-Player" : "2: Multi-Player";
            d_wf.Add("targetFolder", targetFolder);
            // delete the file if it exists.
            if (File.Exists(saveFileLoc))
                File.Delete(saveFileLoc);
            // generate the file and set mode/access types
            FileStream weapStream = new FileStream(saveFileLoc, FileMode.OpenOrCreate, FileAccess.Write);
            // now to write the file
            using (StreamWriter file = new StreamWriter(weapStream))
            {
                file.WriteLine();
                file.WriteLine("{");
                file.WriteLine("\t\"{0}\" ( \"{1}\" )", weaponName, configstringGDFType);
                file.WriteLine("\t{");
                foreach (KeyValuePair<string, string> kvp in d_wf)
                    file.WriteLine("\t\t\"{0}\" \"{1}\"", kvp.Key, kvp.Value);
                file.WriteLine("\t}");
                file.WriteLine("}");
                file.Close();
            }
            weapStream.Close();
        }
        private static string ConcactNotetrack(string notetracks)
        {
            // split at every \r\n instance
            string[] s_tracks = notetracks.Split('\r', '\n');
            string combined = "";
            // go through loop
            for (int i = 0; i < s_tracks.Length; i++)
            {
                // ignore the track if it's empty (this shouldn't happen w/ ported weapons)
                if (String.IsNullOrEmpty(s_tracks[i]))
                    continue;
                //Console.WriteLine(s_tracks[i]);
                // get our entire track name since we don't split at a space, notice below
                string total_track = s_tracks[i];
                // now we split into: xanim_track[0] soundalias_track[1]
                string[] track = total_track.Split(' ');
                track[1] += " \\r\\n";
                // concat the strings together
                combined += track[0] + " " + track[1];
            }
            return combined;
        }
        private static string FixEffectsPath(string path)
        {
            // set generic fx path
            string m_path = @"fx\\";
            // concat the string with replaces / with \\ and attach extension
            m_path += path.Replace(@"/", @"\\") + ".efx";
            return m_path;
        }
        private static string DetermineGDFType(string weaponClass)
        {
            string GDFType = "";
            string GDFExtn = ".gdf";
            switch(weaponClass)
            {
                case "grenade":
                    GDFType = "grenadeweapon";
                    break;
                default:
                    GDFType = "bulletweapon";
                    break;
            }
            return GDFType + GDFExtn;
        }
        // 1.0.3 - streamlined console messages 
        private static void WriteConsole(string message, bool abort)
        {
            Console.WriteLine(message);
            if (abort)
            {
                Console.WriteLine("Aborting...");
                Thread.Sleep(2000);
                Environment.Exit(1);
            }
        }
    }
}