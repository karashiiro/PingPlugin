namespace PingPlugin
{
    public static class TreeResult
    {
        public static TreeResult<T> Resolve<T>(T result)
        {
            return TreeResult<T>.Resolve(result);
        }
    }

    public class TreeResult<T>
    {
        public bool Completed { get; init; }

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

        public static TreeResult<T> Resolve(T result)
        {
            return new TreeResult<T> { Value = result, Completed = true };
        }

        public static implicit operator TreeResult<T>(bool b) => b ? Pass() : Fail();
    }
}