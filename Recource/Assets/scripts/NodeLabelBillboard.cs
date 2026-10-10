using UnityEngine;

/// <summary>
/// Billboards a TextMesh label so it always faces the camera and reads
/// correctly (left-to-right, glyphs not mirrored).
///
/// The trick: we need +Z toward camera (front face), +X = cam.right (reading),
/// +Y = cam.up (top). These form a LEFT-handed basis, so no pure rotation
/// can achieve it. We use a Z-axis scale flip to convert the right-handed
/// rotation into the left-handed result we need.
///
///   LookRotation(cam.forward, cam.up):
///     +X = cam.right  ✓ (correct reading)
///     +Y = cam.up     ✓ (correct top)
///     +Z = cam.forward (into screen → back face)
///
///   localScale.z = -1 flips +Z to -cam.forward (toward camera → front face).
///   Since the glyphs live in the XY plane, the Z-flip does NOT affect their
///   shape or reading order — it only changes which side faces the camera.
/// </summary>
public class NodeLabelBillboard : MonoBehaviour
{
    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) cam = Camera.current;
        if (cam == null) return;

        // Set up the right-handed basis: +X=cam.right, +Y=cam.up, +Z=cam.forward
        transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);

        // Flip Z to bring the front face (+Z) toward the camera.
        // This does NOT mirror the glyphs (they're in the XY plane).
        transform.localScale = new Vector3(1f, 1f, -1f);
    }
}
