namespace SpeakEase.Write.Infrastructure.Exceptions
{
    public sealed class BusinessExceptions : Exception
    {
        public BusinessExceptions()
        {
        }

        public BusinessExceptions(string message) : base(message)
        {
        }
    }

    public static class BusinessThrow
    {
        public static void ThrowException(string message)
        {
            throw new BusinessExceptions(message);
        }
    }
}
