using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace CarbonStories
{
    [Serializable]
    public class GameConfiguration
    {
        public float questionWaitTime = 60f;
        public List<string> tutorialVideos = new List<string>();
        public List<string> resultVideos = new List<string>();

        public string TutorialVideo(int index) => tutorialVideos != null && index < tutorialVideos.Count
            ? tutorialVideos[index] : $"Tutorials/{index}.mp4";
        public string ResultVideo(int index) => resultVideos != null && index < resultVideos.Count
            ? resultVideos[index] : $"Results/{index}.mp4";
    }

    [Serializable]
    public class GradeDescription
    {
        public string grade;
        public string description;
    }

    [Serializable]
    public class ResultDescriptions
    {
        public List<GradeDescription> grades = new List<GradeDescription>();

        public string GetDescription(string grade) => grades?.Find(entry => entry != null && entry.grade == grade)?.description ?? string.Empty;
    }

    public static class ConfigurationReader
    {
        public static IEnumerator Load<T>(string relativePath, Action<T> onLoaded) where T : class
        {
            string path = Application.streamingAssetsPath.TrimEnd('/', '\\') + "/" + relativePath;
            if (!path.Contains("://") && !path.StartsWith("jar:"))
                path = new Uri(path).AbsoluteUri;
            using (UnityWebRequest request = UnityWebRequest.Get(path))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Could not load {relativePath}: {request.error}. Using defaults.");
                    yield break;
                }
                T data;
                try
                {
                    data = JsonConvert.DeserializeObject<T>(request.downloadHandler.text);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Could not parse {relativePath}: {exception.Message}. Using defaults.");
                    yield break;
                }
                if (data != null)
                    onLoaded(data);
            }
        }
    }
}
