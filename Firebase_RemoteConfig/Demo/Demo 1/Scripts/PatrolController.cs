using UnityEngine;
using Firebase.Unity;

namespace Firebase.ConfigAutoSync.Demo
{
  public class PatrolController : MonoBehaviour
  {
    public PatrolBehaviour PatrollerPrefab;
    public Transform[] Points;

    public void CreatePatrol() {
      var patrol = Instantiate(PatrollerPrefab);
      patrol.Points = Points;
    }

    public void ForceRefresh() {
      FirebaseInitializer.RemoteConfigActivateFetched(() => { }, true);
    }
  }
}