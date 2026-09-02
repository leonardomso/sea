using System;
using System.IO;
using UnityEngine;

namespace Sea.Client
{
    public static class SeaEvidenceWriter
    {
        public static void Write(string path, object evidence)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Evidence path is required.", nameof(path));
            }

            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            var json = JsonUtility.ToJson(evidence, true);
            Debug.Log($"SEA_EVIDENCE_JSON={JsonUtility.ToJson(evidence)}");
#if UNITY_WEBGL && !UNITY_EDITOR
            return;
#else
            var absolutePath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(absolutePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("Evidence path has no directory.");
            }

            Directory.CreateDirectory(directory);
            File.WriteAllText(absolutePath, json);
#endif
        }
    }
}
