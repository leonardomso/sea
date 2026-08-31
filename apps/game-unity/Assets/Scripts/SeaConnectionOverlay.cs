using UnityEngine;

namespace Sea.Client
{
    public sealed class SeaConnectionOverlay : MonoBehaviour
    {
        [SerializeField] private SeaConnectionController connection;

        private void Awake()
        {
            if (connection == null)
            {
                connection = FindFirstObjectByType<SeaConnectionController>();
            }
        }

        private void OnGUI()
        {
            const int margin = 24;
            const int width = 430;
            const int height = 176;

            GUI.Box(new Rect(margin, margin, width, height), "SEA // LOCAL CONNECTION");
            if (connection == null)
            {
                GUI.Label(new Rect(margin + 16, margin + 34, width - 32, 28), "Connection controller missing");
                return;
            }

            GUI.Label(new Rect(margin + 16, margin + 34, width - 32, 24), "Status: " + connection.Status);
            GUI.Label(new Rect(margin + 16, margin + 60, width - 32, 24), "Server: " + connection.ServerUrl);
            GUI.Label(new Rect(margin + 16, margin + 86, width - 32, 24), "Database: " + connection.DatabaseName);

            if (GUI.Button(new Rect(margin + 16, margin + 120, 118, 32), "Reconnect"))
            {
                connection.Disconnect();
                connection.Connect();
            }

            if (GUI.Button(new Rect(margin + 146, margin + 120, 118, 32), "Disconnect"))
            {
                connection.Disconnect();
            }
        }
    }
}
