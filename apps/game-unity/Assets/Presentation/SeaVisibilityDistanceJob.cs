using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Sea.Client
{
    [BurstCompile]
    public struct SeaVisibilityDistanceJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> Positions;
        [WriteOnly] public NativeArray<float> SquaredDistances;
        public float2 Origin;

        public void Execute(int index)
        {
            var offset = Positions[index] - Origin;
            SquaredDistances[index] = math.lengthsq(offset);
        }
    }
}
