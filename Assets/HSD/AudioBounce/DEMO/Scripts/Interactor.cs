using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HSD.AudioBounce.Demo
{
    [RequireComponent(typeof(Collider))]
    public class Interactor : MonoBehaviour
    {
        public string objectName1;
        public string objectName2;
        public KeyCode interactionKey = KeyCode.E; // Default to "E"
        public KeyCode playNonLoopingSound = KeyCode.P; // Default to "P"

        private Collider _collider;
        private GameObject _currentObjectInside;

        private List<AudioSource> _loopingSources = new List<AudioSource>();
        private List<AudioSource> _nonLoopingSources = new List<AudioSource>();

        private void Start()
        {
            _collider = GetComponent<Collider>(); 
#if UNITY_6000
            var _audioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).ToList();
#else   
            var _audioSources = FindObjectsOfType<AudioSource>().ToList();
#endif
            foreach (var source in _audioSources)
            {
               if(source.loop)
                    _loopingSources.Add(source);
               else
                    _nonLoopingSources.Add(source);
            }
            
            EventBroadcaster.Broadcast("Shutdown");


            if (!_collider.isTrigger)
            {
                Debug.LogWarning("Collider on Interactor should be set to 'Is Trigger' for optimal behavior.");
            }
        }

        private void Update()
        {
            if (_currentObjectInside != null && Input.GetKeyDown(interactionKey))
            {
                if (_currentObjectInside.name == objectName1)
                {
                    LeverMethod(_currentObjectInside);
                }
                else if (_currentObjectInside.name == objectName2)
                {
                    SwitchMethod(_currentObjectInside);
                }
            }
            
            if (Input.GetKeyDown(playNonLoopingSound))
            {
                int random = Random.Range(0, _nonLoopingSources.Count);
                AudioSource source = _nonLoopingSources[random];
                EventBroadcaster.Broadcast(source.name);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.name == objectName1 || other.gameObject.name == objectName2)
            {
                _currentObjectInside = other.gameObject;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.gameObject == _currentObjectInside)
            {
                _currentObjectInside = null;
            }
        }

        private string randomName;

        private void LeverMethod(GameObject gameObject)
        {
            Animation anim = gameObject.GetComponent<Animation>();
            if (anim.isPlaying)
                return;
            EventBroadcaster.Broadcast("Shutdown");

            int random = Random.Range(0, _loopingSources.Count);
            AudioSource source = _loopingSources[random];

            EventBroadcaster.Broadcast(source.name);
            anim.Play();
            Debug.Log($"Interacted with object named: {objectName1}");
        }

        private void SwitchMethod(GameObject gameObject)
        {

            Animator animator = gameObject.GetComponent<Animator>();

            if (animator.GetBool("Up") == false)
            {
                animator.SetBool("Up", true);
                _loopingSources.ForEach(source => EventBroadcaster.Broadcast(source.name));
            }
            else
            {
                animator.SetBool("Up", false);
                EventBroadcaster.Broadcast("Shutdown");
            }

            Debug.Log($"Interacted with object named: {objectName2}");
        }
    }
}