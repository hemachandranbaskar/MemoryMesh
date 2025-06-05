using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

public class FetchMetaphorImage : MonoBehaviour
{
    [Header("API Configuration")]
    public string apiUrl = "http://127.0.0.1:8000/generate";
    public string todoText = "study at night"; //Buy groceries";

    [Header("Prefab Setup")]
    public GameObject visualPrefab; // Assign your quad prefab
    public Transform spawnPoint;
    //private Material baseUnlitMaterial;

    [ContextMenu("Test With Sample Prompt")]
    public void TestPrompt()
    {
        string samplePrompt = "study at night";
        StartCoroutine(SendPromptToAPI(samplePrompt));
    }

    public void GenerateVisualForTodo(string todo)
    {
        StartCoroutine(SendPromptToAPI(todo));
    }

    IEnumerator SendPromptToAPI(string prompt)
    {
        var requestBody = JsonUtility.ToJson(new PromptRequest { prompt = prompt });

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            byte[] jsonToSend = Encoding.UTF8.GetBytes(requestBody);
            request.uploadHandler = new UploadHandlerRaw(jsonToSend);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"API Error: {request.error}");
            }
            else
            {
                string jsonResponse = request.downloadHandler.text;
                var result = JsonUtility.FromJson<PromptResponse>(jsonResponse);

                string base64 = result.image_data.data[0].base64;
                Texture2D tex = DecodeImage(base64);
                SpawnVisual(tex, result.prompt);
            }
        }
    }

    Texture2D DecodeImage(string base64)
    {
        byte[] imageBytes = Convert.FromBase64String(base64);
        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(imageBytes);
        return tex;
    }

    void SpawnVisual(Texture2D texture, string promptText)
    {
        Vector3 spawnPos = spawnPoint ? spawnPoint.position : Vector3.zero;
        GameObject obj = Instantiate(visualPrefab, spawnPos, Quaternion.identity);

        // Apply texture to quad
        Renderer renderer = obj.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            //renderer.material = new Material(Shader.Find("Unlit/Texture"));
            //renderer.material.mainTexture = texture;
            var baseUnlitMaterial = renderer.material;
            Material dynamicMat = new Material(baseUnlitMaterial); // baseUnlitMaterial = your assigned material
            dynamicMat.mainTexture = texture;
            renderer.material = dynamicMat;
        }

        // Apply metaphor text if TextMeshPro is present
        TextMeshPro tmp = obj.GetComponentInChildren<TextMeshPro>();
        if (tmp != null)
        {
            tmp.text = promptText;
        }
    }

    [Serializable]
    public class PromptRequest
    {
        public string prompt;
    }

    [Serializable]
    public class PromptResponse
    {
        public string prompt;
        public string metaphor_prompt;
        public ImageData image_data;
    }

    [Serializable]
    public class ImageData
    {
        public ImageBase64Entry[] data;
    }

    [Serializable]
    public class ImageBase64Entry
    {
        public string base64;
    }
}
