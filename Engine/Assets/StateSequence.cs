using System;

namespace InventoryInventor
{
    public class StateSequence
    {
        private int _currentState = 0;

        /// <summary>
        /// Get the next available State
        /// </summary>
        /// <returns></returns>
        /// <exception cref="OverflowException"></exception>
        public int Next()
        {
            if (++_currentState > 255)
            {
                throw new OverflowException("State cannot be larger than 255");
            }

            return _currentState;
        }
    }
}