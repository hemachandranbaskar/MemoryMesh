// MemoryAnchorManager.cs - Combined anchor + API + visual spawn manager using Meta SDK

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using Meta.XR.BuildingBlocks;
using Unity.XR.CoreUtils;

public class MemoryAnchorManager : MonoBehaviour
{
    [SerializeField] private SpatialAnchorCoreBuildingBlock _spatialAnchorCore;

    private const string NumUuidsPlayerPref = "numUuids";

    [Header("API Config")]
    public string apiUrl = "http://127.0.0.1:8000/generate";

    [Header("Prefab Setup")]
    public GameObject visualPrefab;
    public Transform spawnPoint;
    public MicRecorder recorder;
    [SerializeField] private GroqTTS ttsSpeaker;

    private string _pendingImage;
    private string _pendingPrompt;
    private GameObject _lastPreviewAnchor;

    private void Start()
    {
        if (_spatialAnchorCore == null)
        {
            Debug.LogError("SpatialAnchorCoreBuildingBlock is not assigned in the Inspector.");
            return;
        }
        _spatialAnchorCore.OnAnchorCreateCompleted.AddListener(OnAnchorCreated);
        _spatialAnchorCore.OnAnchorEraseCompleted.AddListener(RemoveAnchorFromLocalStorage);
    }

    // Trigger this on controller input or button to place anchor + generate image
    public void HandleTodoAnchor(string todoText)
    {
        if (recorder != null && !string.IsNullOrEmpty(recorder.LastTranscription))
        {
            StartCoroutine(SendPromptToAPI(recorder.LastTranscription));
        }
        else
        {
            Debug.LogWarning("No transcription found or recorder not assigned. Sending default prompt...");
            StartCoroutine(SendPromptToAPI(todoText));
        }
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
                Debug.LogError("API Error: " + request.error);
            }
            else
            {
                string jsonResponse = request.downloadHandler.text;
                var result = JsonUtility.FromJson<PromptResponse>(jsonResponse);

                string base64 = result.image_data.data[0].base64;
                //string prompt = result.prompt;

                // Place anchor and pass image/metaphor later in OnAnchorCreated
                // _spatialAnchorCore.CreateSpatialAnchor() doesn't exist in SDK
                // Instead, trigger anchor creation using SDK's placement flow externally
                Debug.Log("[MemoryAnchorManager] Ready to create anchor - call SDK CreateAnchor externally with _pendingImage + _pendingMetaphor.");

                // Cache for use during OnAnchorCreated
                _pendingImage = base64;
                _pendingPrompt = prompt;

                Texture2D tex = DecodeImage(base64);
                StartCoroutine(TryApplyVisualToPreview(tex, prompt));
            }
        }
    }

    private IEnumerator TryApplyVisualToPreview(Texture2D tex, string prompt)
    {
        float timeout = 50f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            var spawner = FindFirstObjectByType<SpatialAnchorSpawnerBuildingBlock>();
            if (spawner != null && spawner.AnchorPrefab != null)
            {
                var previews = GameObject.FindGameObjectsWithTag("AnchorPreview");

                foreach (var preview in previews)
                {
                    if (preview != _lastPreviewAnchor)// && preview.GetComponent<OVRSpatialAnchor>())
                    {
                        _lastPreviewAnchor = preview;
                        ApplyVisualToAnchorObject(preview, tex, prompt);
                        yield break;
                    }
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning("Could not find preview anchor to apply texture.");
    }

    public void OnAnchorCreated(OVRSpatialAnchor anchor, OVRSpatialAnchor.OperationResult result)
    {
        //if (result != OVRSpatialAnchor.OperationResult.Success) return;

        int index = PlayerPrefs.GetInt(NumUuidsPlayerPref, 0);
        string keyPrefix = "uuid" + index;

        string filePath = Application.persistentDataPath + $"/image_{anchor.Uuid}.png";
        File.WriteAllBytes(filePath, Convert.FromBase64String(_pendingImage));

        PlayerPrefs.SetString(keyPrefix, anchor.Uuid.ToString());
        //PlayerPrefs.SetString(keyPrefix + "_image", _pendingImage);
        PlayerPrefs.SetString(keyPrefix + "_image_path", filePath);
        PlayerPrefs.SetString(keyPrefix + "_prompt", _pendingPrompt);

        PlayerPrefs.SetInt(NumUuidsPlayerPref, index + 1);

        GameObject anchorObj = anchor.gameObject;
        Texture2D tex = DecodeImage(_pendingImage);
        ApplyVisualToAnchorObject(anchorObj, tex, _pendingPrompt);

        if (anchorObj.CompareTag("AnchorPreview"))
        {
            anchorObj.tag = "PlacedAnchor";
        }

        if (ttsSpeaker != null)
        {
            _ = ttsSpeaker.GenerateAndPlaySpeech($"Anchor placed for {_pendingPrompt}.  What would you like to remember next?");
        }

        _pendingImage = null;
        _pendingPrompt = null;
    }


    public void OnAnchorLoaded(OVRSpatialAnchor anchor, OVRSpatialAnchor.OperationResult result, GameObject anchorObject)
    {
        //if (result != OVRSpatialAnchor.OperationResult.Success) return;

        string uuid = anchor.Uuid.ToString();
        int count = PlayerPrefs.GetInt(NumUuidsPlayerPref, 0);

        for (int i = 0; i < count; i++)
        {
            string path = PlayerPrefs.GetString("uuid" + i + "_image_path", "");
            if (PlayerPrefs.GetString("uuid" + i) == uuid)
            {
                //string base64 = PlayerPrefs.GetString("uuid" + i + "_image", "");
                string prompt = PlayerPrefs.GetString("uuid" + i + "_prompt", ""); 
                if (File.Exists(path))
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    Texture2D tex = new Texture2D(2, 2);
                    tex.LoadImage(bytes);
                    ApplyVisualToAnchorObject(anchor.gameObject, tex, prompt);
                }
                //string prompt = PlayerPrefs.GetString("uuid" + i + "_prompt", "");
                //Texture2D tex = DecodeImage(base64);
                //ApplyVisualToAnchorObject(anchorObject, tex, prompt);
                break;
            }
        }
    }

    private void ApplyVisualToAnchorObject(GameObject anchorObject, Texture2D texture, string prompt)
    {
        Renderer renderer = anchorObject.GetComponentInChildren<Renderer>();
        if (renderer)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.mainTexture = texture;
            renderer.material = mat;
        }

        TextMeshProUGUI tmp = anchorObject.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            tmp.gameObject.SetActive(true);
            tmp.text = prompt;
        }
    }


    public void RemoveAnchorFromLocalStorage(OVRSpatialAnchor anchor, OVRSpatialAnchor.OperationResult result)
    {
        string uuid = anchor.Uuid.ToString();
        int count = PlayerPrefs.GetInt(NumUuidsPlayerPref, 0);

        for (int i = 0; i < count; i++)
        {
            if (PlayerPrefs.GetString("uuid" + i) == uuid)
            {
                int last = count - 1;

                PlayerPrefs.SetString("uuid" + i, PlayerPrefs.GetString("uuid" + last));
                PlayerPrefs.SetString("uuid" + i + "_image", PlayerPrefs.GetString("uuid" + last + "_image"));
                PlayerPrefs.SetString("uuid" + i + "_prompt", PlayerPrefs.GetString("uuid" + last + "_prompt"));

                PlayerPrefs.DeleteKey("uuid" + last);
                PlayerPrefs.DeleteKey("uuid" + last + "_image");
                PlayerPrefs.DeleteKey("uuid" + last + "_prompt");

                PlayerPrefs.SetInt(NumUuidsPlayerPref, last);
                break;
            }
        }
    }

    private Texture2D DecodeImage(string base64)
    {
        byte[] imageBytes = Convert.FromBase64String(base64);
        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(imageBytes);
        return tex;
    }

    private void SpawnVisual(Texture2D texture, string prompt, Vector3 position)
    {
        GameObject obj = Instantiate(visualPrefab, position, Quaternion.identity);
        Renderer renderer = obj.GetComponentInChildren<Renderer>();

        if (renderer)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.mainTexture = texture;
            renderer.material = mat;
        }

        TextMeshProUGUI tmp = obj.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            tmp.gameObject.SetActive(true);
            tmp.text = prompt;
        }
    }

    [Serializable]
    public class PromptRequest
    {
        public string prompt;
        public bool generate_metaphor = true;
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
