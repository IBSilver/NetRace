using System.Collections.Generic;
using UnityEngine;

public class ObjectManager : MonoBehaviour
{
    [System.Serializable]
    public class Hammer
    {
        public GameObject hammerObject;
        public bool startMovingRight = true;
    }

    [System.Serializable]
    public class Platform
    {
        public GameObject platformObject;
        public bool startMovingRight = true;
    }

    public List<Hammer> hammers = new List<Hammer>();
    public List<Platform> platforms = new List<Platform>();

    public float hammerSwingSpeed = 1.0f;
    public float hammerSwingAngle = 45.0f;

    public float platformMoveSpeed = 2.0f;
    public float platformMoveDistance = 5.0f;

    private Dictionary<GameObject, Vector3> platformStartPositions;

    void Start()
    {
        platformStartPositions = new Dictionary<GameObject, Vector3>();

        foreach (var platform in platforms)
        {
            if (platform.platformObject != null)
            {
                platformStartPositions[platform.platformObject] = platform.platformObject.transform.position;
            }
        }
    }

    void Update()
    {
        MoveHammers();
        MovePlatforms();
    }

    void MoveHammers()
    {
        foreach (var hammer in hammers)
        {
            if (hammer.hammerObject != null)
            {
                float direction = hammer.startMovingRight ? 1.0f : -1.0f;

                float swingAngle = Mathf.Sin(Time.time * hammerSwingSpeed) * hammerSwingAngle * direction;
                hammer.hammerObject.transform.localRotation = Quaternion.Euler(swingAngle, 0, 0);
            }
        }
    }

    void MovePlatforms()
    {
        foreach (var platform in platforms)
        {
            if (platform.platformObject != null && platformStartPositions.ContainsKey(platform.platformObject))
            {
                float direction = platform.startMovingRight ? 1.0f : -1.0f;

                Vector3 startPosition = platformStartPositions[platform.platformObject];
                float offset = Mathf.Sin(Time.time * platformMoveSpeed) * platformMoveDistance * direction;
                platform.platformObject.transform.position = startPosition + new Vector3(offset, 0, 0);
            }
        }
    }
}
