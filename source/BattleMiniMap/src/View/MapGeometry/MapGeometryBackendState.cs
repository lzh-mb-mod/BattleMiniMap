namespace BattleMiniMap.View.MapGeometry
{
    // Counts visible updates only. A valid target is scheduling evidence, not pixel readback.
    internal sealed class MapGeometryBackendState
    {
        internal const int WarmupUpdates = 2, StallUpdates = 180;
        private int _age, _lastPaintCount, _unchanged;
        private bool _selected;
        public string Failure { get; private set; }
        public void Reset() { _age = _lastPaintCount = _unchanged = 0; _selected = false; Failure = null; }
        public bool Update(int paintCount, bool valid, string error, bool reuseCached = false)
        {
            if (Failure != null) return false;
            ++_age;
            if (error != null) { Failure = error; return false; }
            // Parked, valid targets do not owe callbacks. A new draw request gets
            // its own visible progress window rather than inheriting idle age.
            _unchanged = (valid && reuseCached && paintCount >= 2) || paintCount > _lastPaintCount ? 0 : _unchanged + 1;
            _lastPaintCount = paintCount;
            if (_selected && !valid) Failure = "GPU target became invalid";
            else if (!(reuseCached && paintCount >= 2) && _unchanged >= StallUpdates) Failure = "No GPU paint progress during visible updates";
            else if (_age >= StallUpdates && !valid) Failure = "GPU target did not become valid";
            if (Failure != null) return false;
            _selected = valid && paintCount >= 2 && _age >= WarmupUpdates;
            return _selected;
        }
    }
}
