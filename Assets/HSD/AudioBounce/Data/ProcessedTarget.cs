using System;

namespace HSD.AudioBounce.Data
{
    [Serializable]
    public class ProcessedTarget
    {
        public int targetIndex;
        public int cycles;

        public ProcessedTarget(int targetIndex, int cycles)
        {
            this.targetIndex = targetIndex;
            this.cycles = cycles;
        }
    }
}