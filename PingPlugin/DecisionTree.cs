using System;

namespace PingPlugin
{
    public class DecisionTree<TReturn>
    {
        private readonly Func<TreeResult<TReturn>> resultFn;
        private readonly DecisionTree<TReturn> pass;
        private readonly DecisionTree<TReturn> fail;

        public DecisionTree(Func<TreeResult<TReturn>> resultFn, DecisionTree<TReturn> pass = null, DecisionTree<TReturn> fail = null)
        {
            this.resultFn = resultFn ?? throw new ArgumentNullException(nameof(resultFn));
            this.pass = pass;
            this.fail = fail;
        }

        public TReturn Execute()
        {
            return Execute(0);
        }

        private TReturn Execute(int level)
        {
            var result = this.resultFn();

            if (result.Completed)
            {
                return (TReturn)result.Value;
            }
            
            switch (result.Value)
            {
                case true:
                    if (this.pass == null)
                    {
                        throw new ArgumentNullException($"Decision at level {level} returned true, but {nameof(pass)} branch was null!");
                    }

                    return this.pass.Execute(level);
                case false:
                    if (this.pass == null)
                    {
                        throw new ArgumentNullException($"Decision at level {level} returned true, but {nameof(fail)} branch was null!");
                    }

                    return this.fail.Execute(level);
                default:
                    throw new InvalidOperationException("TreeResult is not completed, but has a non-boolean value.");
            }
        }
    }
}