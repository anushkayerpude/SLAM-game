using UnityEngine;
using NightWarden.Core;

namespace NightWarden.Objectives
{
    /// <summary>
    /// Extraction beacon activated after all 3 bombs are defused.
    /// Walking into this extraction zone secures victory.
    /// </summary>
    public class ExtractionPoint : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                GameManager.Instance?.TriggerExtractionReached();
            }
        }
    }
}
