using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class BonsaiEditorMenu : MonoBehaviour
{
    [MenuItem("Bonsai/Create Bonsai workflow asset")]
    static void CreateBonsaiWorkflowAsset()
    {
        string path = EditorUtility.OpenFilePanelWithFilters("Choose workflow file", "", new string[] { "Bonsai Workflow (.bonsai)", "bonsai" });

        string fileData = File.ReadAllText(path);

        string workflowName = path.Split('/').Last();

        // Generate asset
        BonsaiWorkflowAsset asset = ScriptableObject.CreateInstance<BonsaiWorkflowAsset>();
        asset.WorkflowBuilderData = fileData;

        if (!File.Exists($"{Application.dataPath}/{workflowName}.asset"))
        {
            AssetDatabase.CreateAsset(asset, $"Assets/{workflowName}.asset");
            AssetDatabase.SaveAssets();
        }
        else
        {
            Debug.LogWarning($"An asset with the name {workflowName} already exists.");
        }
    }
}
