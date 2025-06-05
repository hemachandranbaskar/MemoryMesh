using UnityEngine;

public class AnchorResolverNotifier : MonoBehaviour
{
    private OVRSpatialAnchor _anchor;
    private bool _notified = false;

    void Start()
    {
        _anchor = GetComponent<OVRSpatialAnchor>();
    }

    void Update()
    {
        if (!_notified && _anchor && _anchor.Created)
        {
            _notified = true;

            var manager = GetComponent<MemoryAnchorManager>();
            if (manager)
            {
                manager.OnAnchorLoaded(_anchor, OVRSpatialAnchor.OperationResult.Success, this.gameObject);
            }
        }
    }
}
