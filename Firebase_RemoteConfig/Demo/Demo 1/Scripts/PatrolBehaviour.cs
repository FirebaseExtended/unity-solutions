using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Firebase.ConfigAutoSync.Demo {
  [RequireComponent(typeof(Renderer))]
  public class PatrolBehaviour : MonoBehaviour {
    public Transform[] Points;
    public int StartPoint;
    public int NumPoints;
    public double Speed = 1.0;
    public double Size = 1.0;

    private RemoteConfigSyncBehaviour syncBehaviour;
    private Renderer renderer;
    private Transform destination;
    private int destinationInd;

    private void Start() {
      syncBehaviour = GetComponent<RemoteConfigSyncBehaviour>();
      if (Points.Length == 0 || syncBehaviour == null) {
        gameObject.SetActive(false);
        return;
      }
      // Disable rendering until SyncComplete.
      renderer = GetComponent<Renderer>();
      renderer.enabled = false;
      syncBehaviour.SyncComplete += Activate;
    }

    private void Activate(object sender, EventArgs e) {
      renderer.enabled = true;
      if (StartPoint >= Points.Length) {
        StartPoint = 0;
      }

      if (NumPoints > 0 && NumPoints < Points.Length) {
        if (StartPoint + NumPoints < Points.Length) {
          Points = Points.ToList().GetRange(StartPoint, NumPoints).ToArray();
        } else {
          var points = Points.ToList().GetRange(StartPoint, Points.Length - StartPoint);
          points.AddRange(Points.ToList().GetRange(0, NumPoints - points.Count));
          Points = points.ToArray();
        }
      }

      transform.position = Points[0].position;
      destinationInd = (0 + 1) % Points.Length;
      destination = Points[destinationInd];

      transform.localScale *= (float)Size;
      StartCoroutine(Patrol());
    }

    private IEnumerator Patrol() {
      Debug.Log("Starting patrol.");
      while (true) {
        transform.position = Vector3.MoveTowards(transform.position, destination.position, (float)Speed * Time.deltaTime);

        if (Vector3.SqrMagnitude(transform.position - destination.position) < .1f) {
          destinationInd = (destinationInd + 1) % Points.Length;
          destination = Points[destinationInd];
        }
        yield return 1;
      }
    }
  }
}
