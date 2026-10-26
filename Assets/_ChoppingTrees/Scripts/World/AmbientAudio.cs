using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>Forest ambience: a wind bed plus birds calling from random spots around the listener.</summary>
    public class AmbientAudio : MonoBehaviour
    {
        [SerializeField] Vector2 m_BirdInterval = new Vector2(3f, 9f);

        float m_NextBird;

        void Start()
        {
            if (AudioManager.Instance != null)
            {
                var wind = AudioManager.Instance.CreateLoop("wind", AudioManager.Instance.transform, 0.45f);
                if (wind != null) wind.spatialBlend = 0f;
            }
            m_NextBird = Random.Range(1f, 3f);
        }

        void Update()
        {
            m_NextBird -= Time.deltaTime;
            if (m_NextBird > 0f) return;
            m_NextBird = Random.Range(m_BirdInterval.x, m_BirdInterval.y);
            var listener = CameraFx.Instance != null && CameraFx.Instance.isActiveAndEnabled ? CameraFx.Instance.transform : Camera.main != null ? Camera.main.transform : null;
            if (listener == null) return;
            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(10f, 25f);
            Vector3 p = listener.position + new Vector3(r.x, Random.Range(4f, 9f), r.y);
            AudioManager.Sfx("bird", p, Random.Range(0.35f, 0.7f), Random.Range(0.9f, 1.15f));
        }
    }
}
