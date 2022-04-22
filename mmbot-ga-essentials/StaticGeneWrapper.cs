namespace MMBotGA.ga
{
    class StaticGeneWrapper<T> : GeneWrapper<T>
    {
        private T _value;

        public StaticGeneWrapper(T value) : base(null, null, -1)
        {
            _value = value;
        }

        public override T Value
        {
            get
            {
                return _value;
            }
        }

        public override void Replace(T value)
        {
            _value = value;
        }
    }
}