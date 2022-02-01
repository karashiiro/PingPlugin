using System;

namespace PingPlugin
{
    public class DecisionTree<TReturn>
    {
        private readonly Func<TreeResult<TReturn>> condition;
        private readonly DecisionTree<TReturn> pass;
        private readonly DecisionTree<TReturn> fail;

        public DecisionTree(Func<TreeResult<TReturn>> condition, DecisionTree<TReturn> pass = null, DecisionTree<TReturn> fail = null)
        {
            this.condition = condition ?? throw new ArgumentNullException(nameof(condition));
            this.pass = pass;
            this.fail = fail;
        }

        public TReturn Execute()
        {
            return Execute(0);
        }

        private TReturn Execute(int level)
        {
            var result = this.condition();
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
                    return (TReturn)result.Value;
            }
        }
    }
}