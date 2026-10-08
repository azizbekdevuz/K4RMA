using UnityEngine;

namespace K4RMA
{
    public class FaceCamera : MonoBehaviour
    {
        void LateUpdate()
        {
            var view = Camera.main;
            if (view == null)
                return;
            transform.rotation = view.transform.rotation;
        }
    }
}
