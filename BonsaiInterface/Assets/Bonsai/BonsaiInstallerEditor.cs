using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Net;
using System.IO;
using System.Diagnostics;
using System;
using System.Xml.Linq;
using System.Linq;

[CustomEditor(typeof(BonsaiInstaller))]
public class BonsaiInstallerEditor : Editor
{
    string RootPath()
    {
        return Application.dataPath.Replace("/Assets", "");
    }

    void DownloadBonsai()
    {
        string rootPath = RootPath();

        if (!File.Exists(rootPath + "/Bonsai.zip"))
        {
            WebClient client = new WebClient();
            client.DownloadFile("https://github.com/bonsai-rx/bonsai/releases/download/2.7/Bonsai.zip", rootPath + "/Bonsai.zip");
        }
        else
        {
            EditorUtility.DisplayDialog("Error", "The Bonsai archive has already been downloaded.", "OK");
        }
    }

    void InstallBonsai()
    {
        string rootPath = RootPath();

        if (!File.Exists(rootPath + "/Bonsai.zip"))
        {
            EditorUtility.DisplayDialog("Error", "Bonsai portable archive not found, please download.", "OK");
            return;
        }

        if (EditorUtility.DisplayDialog("Install Bonsai", "The Bonsai archive will now be extracted and installed. Please close the Bonsai editor window after initialization to complete the install process.", "OK"))
        {
            if (Directory.Exists(rootPath + "/Bonsai"))
            {
                EditorUtility.DisplayDialog("Error", "A Bonsai directory already exists in the project.", "OK");
            }
            else
            {
                var extractProcess = new Process();
                extractProcess.StartInfo.FileName = "powershell.exe";
                extractProcess.StartInfo.Arguments = "Expand-Archive -Force " + rootPath + "/Bonsai.zip";
                extractProcess.EnableRaisingEvents = true;
                extractProcess.Exited += (sender, e) =>
                {
                    var firstLaunch = RunBonsai((sender, e) => { ConsolidatePackages(); });
                    firstLaunch.WaitForExit();
                };
                extractProcess.Start();
                extractProcess.WaitForExit();
            }
        }
    }

    void ConsolidatePackages()
    {
        string rootPath = RootPath();

        if (!Directory.Exists(rootPath + "/Bonsai"))
        {
            EditorUtility.DisplayDialog("Error", "Bonsai directory not found. Please download and install Bonsai.", "OK");
            return;
        }

        // Ensure a packages folder is present
        Directory.CreateDirectory(rootPath + "/Assets/Packages");

        XDocument config = XDocument.Load(rootPath + "/Bonsai/Bonsai.config");
        var assemblyQuery = from c in config.Descendants("AssemblyLocation")
                            where c.Attribute("location").Value.Contains(".dll")
                            select $"{rootPath}/Bonsai/{c.Attribute("location").Value}";

        var libraryQuery = from c in config.Descendants("LibraryFolder")
                           where c.Attribute("platform").Value.Equals("x64")
                           select Directory.EnumerateFiles($"{rootPath}/Bonsai/{c.Attribute("path").Value}");

        var allQueries = assemblyQuery.Concat(libraryQuery.SelectMany(x => x));

        foreach (string fromFile in allQueries)
        {
            string toFile = $"{rootPath}/Assets/Packages/{Path.GetFileName(fromFile)}";
            if (!File.Exists(toFile))
            {
                UnityEngine.Debug.Log($"Copying {fromFile} to {toFile}");
                File.Copy(fromFile, toFile);
            }
            else
            {
                UnityEngine.Debug.LogWarning($"File {toFile} already exists. Skipping file copy.");
            }
        }

        EditorUtility.DisplayDialog("Bonsai", "Package consolidation complete. See debug console for details.", "OK");
    }

    Process RunBonsai(EventHandler onExit)
    {
        string rootPath = RootPath();

        if (File.Exists(rootPath + "/Bonsai/Bonsai.exe"))
        {
            var launchProcess = new Process();
            launchProcess.StartInfo.FileName = "powershell.exe";
            launchProcess.StartInfo.Arguments = "Bonsai/Bonsai.exe";
            launchProcess.EnableRaisingEvents = true;
            launchProcess.Exited += onExit;
            launchProcess.Start();
            return launchProcess;
        }
        else
        {
            EditorUtility.DisplayDialog("Error", "No Bonsai executable could be found, please ensure Bonsai is downloaded and installed.", "OK");
        }

        return null;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        GUI.skin.label.wordWrap = true;

        GUILayout.Space(10);

        // Download Bonsai
        GUILayout.Label("Download the Bonsai portable archive.");
        if (GUILayout.Button("Download"))
        {
            DownloadBonsai();
        }
        GUILayout.Space(10);

        // Extract and install
        GUILayout.Label("Extract and perform first-time install of Bonsai.");
        if (GUILayout.Button("Install"))
        {
            InstallBonsai();
        }
        GUILayout.Space(10);

        // Launch editor
        GUILayout.Label("Launch the Bonsai editor.");
        if (GUILayout.Button("Launch Bonsai"))
        {
            RunBonsai((sender, e) => { UnityEngine.Debug.Log("Bonsai editor closed."); });
        }
        GUILayout.Space(10);

        // Consolidate packages
        GUILayout.Label("Consolidate packages. This will copy the Bonsai environment packages to the Unity project. This is required to reference Bonsai components from Unity scripts and should be performed whenever new packages are added to the Bonsai environment.");
        if (GUILayout.Button("Consolidate packages"))
        {
            ConsolidatePackages();
        }
        GUILayout.Space(10);
    }
}
