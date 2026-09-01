using Sea.Client;
using VContainer;
using VContainer.Unity;

namespace Sea.Bootstrap
{
    public sealed class SeaLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            RegisterIfPresent<SeaConnectionController>(builder);
            RegisterIfPresent<SeaGameController>(builder);
            RegisterIfPresent<SeaChartCameraController>(builder);
            RegisterIfPresent<SeaWorldView>(builder);
            RegisterIfPresent<SeaHudController>(builder);
            RegisterIfPresent<SeaInputController>(builder);
            RegisterIfPresent<SeaRuntimeValidationProbe>(builder);
        }

        private void RegisterIfPresent<T>(IContainerBuilder builder) where T : UnityEngine.Component
        {
            var component = GetComponent<T>();
            if (component != null)
            {
                builder.RegisterComponent(component);
            }
        }
    }
}
