

using System;

namespace Walt.Framework.Quartz
{
    public class  DistributingAttributes:Attribute
    {
        private int _instanceNumber; 

        public DistributingAttributes(int instanceNumber)
        {
            _instanceNumber=instanceNumber;
        }
    }
}