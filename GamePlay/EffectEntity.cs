using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EffectEntity : MonoBehaviour
{
    public float lifeTime;
    public bool spawnRelateToTransform;

    // Use this for initialization
    void Start()
    {
        if (lifeTime >= 0)
            Destroy(gameObject, lifeTime);
    }

    private void OnEnable()
    {
        var particles = GetComponentsInChildren<ParticleSystem>();
        foreach (var particle in particles)
        {
            particle.Play();
        }
        var audioSources = GetComponentsInChildren<AudioSource>();
        foreach (var audioSource in audioSources)
        {
            audioSource.Play();
        }
    }

    private void OnDisable()
    {
        var particles = GetComponentsInChildren<ParticleSystem>();
        foreach (var particle in particles)
        {
            particle.Stop();
        }
        var audioSources = GetComponentsInChildren<AudioSource>();
        foreach (var audioSource in audioSources)
        {
            audioSource.Stop();
        }
    }

    public static void PlayEffect(EffectEntity prefab, Transform transform)
    {
        if (prefab != null)
        {
            var effectEntity = Instantiate(prefab, transform.position, transform.rotation, prefab.spawnRelateToTransform ? transform : null);
            // Just in case the game object might be not activated by default
            effectEntity.gameObject.SetActive(true);
        }
    }
}
