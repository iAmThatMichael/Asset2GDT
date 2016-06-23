// ==========================================================
// Coding projects
// 
// Component: Call of Duty
// Purpose: Generate a GDT from assetfiles
//
// Initial author: DidUknowiPwn
// Started: 2015-07-24
// ==========================================================

using System;
using System.IO;
using System.Windows.Forms;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Asset2GDT
{
    class Program
    {
        private class PropertyInfo
        {
            public string Property { get; set; }
            public string Label { get; set; }
            public string Description { get; set; }
            public double Increment { get; set; }
            public double HoldIncrement { get; set; }
            public Type Type { get; set; }
        }

        private static Dictionary<string, PropertyInfo> KnownProperties { get; set; }

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                // fix
                /*FolderBrowserDialog folderBrowser = new System.Windows.Forms.FolderBrowserDialog();
                if (folderBrowser.ShowDialog() == DialogResult.OK)
                {
                    Console.WriteLine("Working path: {0}", folderBrowser.SelectedPath);
                    StartExport(folderBrowser.SelectedPath);
                }*/
                WriteConsole("ERROR: No asset file supplied. (Drag-and-Drop supported or use parameters.)", true);
            }
            else
                StartExport(args);
        }
        /*private static void StartExport(string location)
        {
            if (CheckIfFolder(location))
                ExportGroupGDT(location);
            else
                ExportSingleGDT(location);
        }*/
        
        private static void StartExport(string[] assets)
        {
            // if we only only have one asset to export, export as a single GDT
            if (assets.Length == 1 && !CheckIfFolder(assets[0]))
                ExportSingleGDT(assets[0]);
            // else we assume it's multiple, export as grouped gdt
            else
                ExportGroupGDT(assets);
        }
        
        private static void ExportSingleGDT(string asset)
        {
            // sorted dictionary to auto sort the data
            SortedDictionary<string, string> d_wf = GetAssetDictionary(asset);
            GenerateSingleGDT(GetAssetName(asset), GetSaveFileLocation(asset, false), d_wf);
        }
       
        private static void ExportGroupGDT(string[] assets)
        {
            assets = (CheckIfFolder(assets[0])) ? Directory.GetFiles(assets[0]) : assets;
            string saveFileLoc = GetSaveFileLocation(Path.GetFullPath(assets[0]), true);
            // delete the file if it exists.
            if (File.Exists(saveFileLoc))
                File.Delete(saveFileLoc);
            FileStream gdtStream = new FileStream(saveFileLoc, FileMode.OpenOrCreate, FileAccess.Write);
            using (StreamWriter file = new StreamWriter(gdtStream))
            {
                file.WriteLine();
                file.WriteLine("{");
                for (int i = 0; i < assets.Length; i++)
                {
                    string asset = assets[i];
                    if (CheckIfBadAsset(asset))
                        continue;
                    // sorted dictionary to auto sort the data
                    SortedDictionary<string, string> d_wf = GetAssetDictionary(asset);
                    WriteAssetData(GetAssetName(asset), DetermineGDFType(d_wf), d_wf, file);
                }
                file.WriteLine("}");
                file.Close();
            }
        }
       
        private static void GenerateSingleGDT(string assetName, string saveFileLoc, SortedDictionary<string, string> d_wf)
        {
            // delete the file if it exists.
            if (File.Exists(saveFileLoc))
                File.Delete(saveFileLoc);
            string configstringGDFType = DetermineGDFType(d_wf);
            // generate the file and set mode/access types
            FileStream gdtStream = new FileStream(saveFileLoc, FileMode.OpenOrCreate, FileAccess.Write);
            // now to write the file
            using (StreamWriter file = new StreamWriter(gdtStream))
            {
                file.WriteLine();
                file.WriteLine("{");
                WriteAssetData(assetName, configstringGDFType, d_wf, file);
                file.WriteLine("}");
                file.Close();
            }
            gdtStream.Close();
        }
        
        private static string GetSaveFileLocation(string file, bool group)
        {
            // get the directory before the file
            DirectoryInfo saveFolder = Directory.GetParent(file);
            //string sourceFolder = (!GetAssetName(file).Contains("_mp")) ? "sp" : "mp";
            string exportName = (group) ? "asset_grouped" : "asset_" + Path.GetFileNameWithoutExtension(file);
            return saveFolder + @"\" + exportName + ".gdt";
        }
      
        private static string[] GetSplitContents(string asset, char delimiter)
        {
            // exit if we have a file with an extension
            if (Path.HasExtension(asset))
                return null;
            // read the file in
            string contents = File.ReadAllText(asset);
            // split at every '\'
            string[] splitContents = contents.Split(delimiter);
            return splitContents;
        }
        
        private static SortedDictionary<string, string> GetAssetDictionary(string asset)
        {
            string assetName = GetAssetName(asset);
            // split the data at every '\'
            string[] splitContents = GetSplitContents(asset, '\\');
            // get the file type (i.e. WEAPONFILE, FLAMETABLEFILE)
            string configstringFileType = splitContents[0];
            var d_wf = new SortedDictionary<string, string>();
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
                // remove the key if it exists already should stop the exception below.
                if (d_wf.ContainsKey(propName))
                    d_wf.Remove(propName);
                // try/catch instead of straight calling in order to catch the error.
                try
                {
                    d_wf.Add(propName, value);
                }
                catch (Exception exception)
                {
                    string output = string.Format("ERROR: Failed to add key {0} value {1}.\nReason: \"{2}\"", propName, value, exception.Message);
                    WriteConsole(output, true);
                }
            }
            // this is added in AssMan
            d_wf.Add("configstringFileType", configstringFileType);
            // also added in AssMan, check if our assetfile is MP/SP and name it accordingly
            string targetFolder = (!assetName.Contains("_mp")) ? "1: Single-Player" : "2: Multi-Player";
            d_wf.Add("targetFolder", targetFolder);
            return d_wf;
        }
         
        private static bool CheckIfBadAsset(string asset)
        {
            string assetName = GetAssetName(asset);
            // split the data at every '\'
            string[] splitContents = GetSplitContents(asset, '\\');
            // check to exit if we don't have a var
            if (splitContents == null || Path.HasExtension(asset))
                return true;
            return false;
        }
       
        private static bool CheckIfFolder(string file)
        {
            // error out if we're exporting as a folder.
            FileAttributes attr = File.GetAttributes(file);
            return ((attr & FileAttributes.Directory) == FileAttributes.Directory);
        }
      
        private static bool CheckIfFolder(string[] assets)
        {
            foreach (string file in assets)
            {
                if (CheckIfFolder(file))
                    return true;
            }
            return false;
        }
        
        private static string GetAssetName(string asset)
        {
            return Path.GetFileNameWithoutExtension(asset);
        }
     
        private static string ConcactNotetrack(string notetracks)
        {
            // split at every \r\n instance
            string[] s_tracks = notetracks.Split('\r', '\n');
            string[] track;
            string combined = "";
            // go through loop
            for (int i = 0; i < s_tracks.Length; i++)
            {
                // ignore the track if it's empty (this shouldn't happen w/ ported assets)
                if (String.IsNullOrEmpty(s_tracks[i]))
                    continue;
                // get our entire track name since we don't split at a space, notice below
                string total_track = s_tracks[i];
                // now we split into: xanim_track[0] soundalias_track[1]
                track = total_track.Split(' ');
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

        private static string DetermineGDFType(SortedDictionary<string, string> d_wf)
        {
            string GDFType = "";
            string assetClass = "";
            // find if weaponType exists if so determine the GDF type as this is a "weapon"
            if (d_wf.TryGetValue("weaponType", out assetClass))
                GDFType = GetGDFTypeFromClass(assetClass);
            // the rest of the types are NOT weaponfiles
            // maybe to-do on cleanup....
            else if (d_wf.TryGetValue("configstringFileType", out assetClass) && assetClass == "FLAMETABLEFILE")
                GDFType = GetGDFTypeFromClass("flame");
            else if (d_wf.TryGetValue("configstringFileType", out assetClass) && assetClass == "BULLET_PEN_TABLE")
                GDFType = GetGDFTypeFromClass("penetration");
            else if (d_wf.TryGetValue("configstringFileType", out assetClass) && assetClass == "LOCDMGTABLE")
                GDFType = GetGDFTypeFromClass("location");
            else
                WriteConsole("WARNING: Couldn't figure out the type of asset!", false);
            return GDFType;
        }

        private static string GetGDFTypeFromClass(string assetClass)
        {
            string GDFType = "";
            //to-do: more assets?
            switch (assetClass)
            {
                case "flame":
                    GDFType = "flametable";
                    break;
                case "gas":
                    GDFType = "gasweapon";
                    break;
                case "grenade":
                    GDFType = "grenadeweapon";
                    break;
                case "location":
                    GDFType = "locdmgtable";
                    break;
                case "penetration":
                    GDFType = "bullet_penetration";
                    break;
                case "projectile":
                    GDFType = "projectileweapon";
                    break;
                default:
                    GDFType = "bulletweapon";
                    break;
            }
            return GDFType + ".gdf";
        }
        
        private static void WriteAssetData(string assetName, string configstringGDFType, SortedDictionary<string, string> d_wf, StreamWriter file)
        {
            // load all GDF properties now that we know what we're looking for
            LoadKnownProperties(configstringGDFType);

            file.WriteLine("\t\"{0}\" ( \"{1}\" )", assetName, configstringGDFType);
            file.WriteLine("\t{");
            foreach (KeyValuePair<string, string> kvp in d_wf)
            {
                // filter them out to know what keys we're looking for
                if (KnownProperties.ContainsKey(kvp.Key))
                    file.WriteLine("\t\t\"{0}\" \"{1}\"", kvp.Key, kvp.Value);
                else
                    WriteConsole("Ignoring an invalid key: " + kvp.Key, false);
            }
            file.WriteLine("\t}");
        }

        // streamlined console messages 
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

        private static void LoadKnownProperties(string type)
        {
            KnownProperties = new Dictionary<string, PropertyInfo>();
            // find the GDF by going to WaW\deffiles.
            DirectoryInfo WaWBin = Directory.GetParent(Registry.GetValue("HKEY_CURRENT_USER\\Software\\iw\\CoDWaWRadiantModTool\\Radiant\\Prefs", "LastProject", null).ToString());
            string GDF = Path.Combine(Directory.GetParent(WaWBin.ToString()).ToString(), "deffiles") + "\\" + type;

            // Read existing properties from the GDF.
            var contents = File.ReadAllText(GDF);

            foreach (Match m in Regex.Matches(contents,
                @"(?<controlType>\w+)\((?<defname>\w+)(?:, ){0,1}(?<extra>[^\)]+){0,1}\)\s+\[(?:[\s\S]*?)label\(""(?<label>[^""]+)""\)(?:(tooltip\(""(?<tooltip>[^""]+)""\)|[^}]))+"))
            {
                var prop = new PropertyInfo
                {
                    Property = m.Groups["defname"].Value,
                    Description = m.Groups["tooltip"].Value,
                    Label = m.Groups["label"].Value
                };

                // Read the "extra" information (increment values)
                if (!string.IsNullOrEmpty(m.Groups["extra"].Value))
                {
                    var extra = m.Groups["extra"].Value.Split(',');
                    if (extra.Length == 2)
                    {
                        prop.Increment = double.Parse(extra[0].Trim());
                        prop.HoldIncrement = double.Parse(extra[1].Trim());
                    }
                }

                // Convert the control types to runtime types.
                switch (m.Groups["controlType"].Value)
                {
                    case "checkbox":
                        prop.Type = typeof(bool);
                        break;
                    case "floatedit":
                        prop.Type = typeof(float);
                        break;
                    case "spinedit":
                        prop.Type = typeof(int);
                        break;
                    default:
                        prop.Type = typeof(string);
                        break;
                }

                KnownProperties.Add(prop.Property, prop);
            }
        }
    }
}