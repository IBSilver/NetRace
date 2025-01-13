using System;
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

    [System.Serializable]
    public class RotatingHammer
    {
        public GameObject hammerObject;
        public float rotationSpeed = 100.0f;
    }

    [System.Serializable]
    public class Roller
    {
        public GameObject rollerObject;
        public float rotationSpeed = 100.0f;
    }

    public List<Hammer> hammers = new List<Hammer>();
    public List<Platform> platforms = new List<Platform>();
    public List<RotatingHammer> rotatingHammers = new List<RotatingHammer>();
    public List<Roller> rollers = new List<Roller>();

    public float hammerSwingSpeed = 1.0f;
    public float hammerSwingAngle = 45.0f;

    public float platformMoveSpeed = 2.0f;
    public float platformMoveDistance = 5.0f;

    private bool startAux = false;

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
        if (IsTimeToStart() || startAux == true)
        {
            startAux = true;
            MoveHammers();
            MovePlatforms();
            RotateHammers();
            RotateRollers();
        }
    }

    bool IsTimeToStart()
    {
        if (string.IsNullOrEmpty(Client.obstacleTime))
        {
            return false;
        }

        DateTime targetTime = ParseObstacleTime(Client.obstacleTime);

        DateTime currentTime = DateTime.Now;

        return currentTime.Hour == targetTime.Hour && currentTime.Minute == targetTime.Minute && currentTime.Second == targetTime.Second;
    }

    DateTime ParseObstacleTime(string timeString)
    {
        string[] timeParts = timeString.Split(':');

        if (timeParts.Length == 4)
        {
            DateTime currentDate = DateTime.Now;

            int hours = int.Parse(timeParts[1]);
            int minutes = int.Parse(timeParts[2]);
            int seconds = int.Parse(timeParts[3]);

            return new DateTime(currentDate.Year, currentDate.Month, currentDate.Day, hours, minutes, seconds);
        }
        else
        {
            throw new ArgumentException("Invalid time format.");
        }
    }

    void MoveHammers()
    {
        foreach (var hammer in hammers)
        {
            if (hammer.hammerObject != null)
            {
                float direction = hammer.startMovingRight ? 1.0f : -1.0f;

                float swingAngle = Mathf.Sin(Time.time * hammerSwingSpeed) * hammerSwingAngle * direction;
                hammer.hammerObject.transform.localRotation = Quaternion.Euler(swingAngle + 180, 90, 0);
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

    void RotateHammers()
    {
        foreach (var rotatingHammer in rotatingHammers)
        {
            if (rotatingHammer.hammerObject != null)
            {
                rotatingHammer.hammerObject.transform.Rotate(0, rotatingHammer.rotationSpeed * Time.deltaTime, 0, Space.Self);
            }
        }
    }

    void RotateRollers()
    {
        foreach (var roller in rollers)
        {
            if (roller.rollerObject != null)
            {
                roller.rollerObject.transform.Rotate(roller.rotationSpeed * Time.deltaTime, 0, 0, Space.Self);
            }
        }
    }
}
