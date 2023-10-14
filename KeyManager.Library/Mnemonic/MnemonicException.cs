﻿namespace Leosac.KeyManager.Library.Mnemonic
{
    public class MnemonicException : Exception
    {
        public MnemonicException()
        {
        }

        public MnemonicException(string message) : base(message)
        {
        }

        public MnemonicException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
