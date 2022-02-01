namespace PingPlugin
{
    public class TreeResult<T>
    {
        public object Value { get; init; }

        internal TreeResult() { }

        public static TreeResult<T> Pass()
        {
            return new TreeResult<T> { Value = true };
        }

        public static TreeResult<T> Fail()
        {
            return new TreeResult<T> { Value = false };
        }

        public static TreeResult<T> FromObject(T result)
        {
            return new TreeResult<T> { Value = result };
        }
    }
}