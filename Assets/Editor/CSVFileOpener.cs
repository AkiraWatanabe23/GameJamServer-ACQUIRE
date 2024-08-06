using System.IO;
using UnityEditor;
using UnityEngine;

public class CSVFileOpener : EditorWindow
{
    private TextAsset _csvFile;

    [MenuItem("Tools/CSV To HTML Converter")]
    public static void ShowWindow()
    {
        GetWindow<CSVFileOpener>("CSV To HTML Converter");
    }

    private void OnGUI()
    {
        GUILayout.Label("CSV to HTML Converter", EditorStyles.boldLabel);
        _csvFile = (TextAsset)EditorGUILayout.ObjectField("CSV File", _csvFile, typeof(TextAsset), false);

        if (GUILayout.Button("Convert to HTML"))
        {
            if (_csvFile != null)
            {
                string csvContent = _csvFile.text;
                string htmlContent = ConvertCSVToHTML(csvContent);
                string filePath = Application.dataPath + "/../" + _csvFile.name + ".html";
                SaveHTMLToFile(htmlContent, filePath);
                Application.OpenURL("file://" + filePath);
                Debug.Log("HTMLファイルを保存しました: " + filePath);
            }
            else
            {
                Debug.LogError("CSVファイルを選択してください");
            }
        }
    }

    private string ConvertCSVToHTML(string csvContent)
    {
        string[] lines = csvContent.Split('\n');
        string html = "<html><body><table border='1'>";

        foreach (string line in lines)
        {
            if (string.IsNullOrEmpty(line)) continue;
            string[] columns = line.Split(',');

            html += "<tr>";
            foreach (string column in columns)
            {
                html += "<td>" + column.Trim() + "</td>";
            }
            html += "</tr>";
        }

        html += "</table></body></html>";
        return html;
    }

    private void SaveHTMLToFile(string htmlContent, string filePath)
    {
        File.WriteAllText(filePath, htmlContent);
    }
}
