using System;

namespace ContainerSearcher
{
    public class ContainerSearcherConfig
    {
        public int SearchRange
        {
            get;
            set
            {
                field = Math.Clamp(value, 1, 10);
            }
        } = 5;
    }
}
