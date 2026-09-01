using Sea.Client;
using UnityEngine;

namespace Sea.Bootstrap
{
    public sealed class SeaSceneComposer : MonoBehaviour
    {
        [SerializeField] private SeaConnectionController connection;
        [SerializeField] private SeaGameController game;
        [SerializeField] private SeaChartCameraController chartCamera;
        [SerializeField] private SeaWorldView world;
        [SerializeField] private SeaHudController hud;
        [SerializeField] private SeaInputController input;
        [SerializeField] private SeaRuntimeValidationProbe validationProbe;
        [SerializeField] private Camera mainCamera;

        public void Configure(
            SeaConnectionController connectionController,
            SeaGameController gameController,
            SeaChartCameraController chartCameraController,
            SeaWorldView worldView,
            SeaHudController hudController,
            SeaInputController inputController,
            SeaRuntimeValidationProbe runtimeValidationProbe,
            Camera camera)
        {
            connection = connectionController;
            game = gameController;
            chartCamera = chartCameraController;
            world = worldView;
            hud = hudController;
            input = inputController;
            validationProbe = runtimeValidationProbe;
            mainCamera = camera;
            Compose();
        }

        private void Awake() => Compose();

        private void Compose()
        {
            chartCamera?.ConfigureDependencies(connection, world);
            game?.ConfigureDependencies(connection, mainCamera);
            world?.ConfigureDependencies(connection);
            validationProbe?.ConfigureDependencies(connection);
            hud?.ConfigureDependencies(connection, game, input);
            input?.ConfigureDependencies(game, chartCamera, hud);
        }
    }
}
