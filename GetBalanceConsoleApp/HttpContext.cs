namespace GetBalanceConsoleApp
{
    internal class HttpContext
    {
        public static HttpContext Current { get; internal set; }
        public object Request { get; internal set; }
        public object Response { get; internal set; }
    }
}