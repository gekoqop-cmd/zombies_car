using UnityEngine;

namespace ZombiCar.Player
{
    /// <summary>
    /// Aim diagnostics (Scene view, or Game view with Gizmos on) while playing: yellow = camera ray through the
    /// cursor, sphere = aim point (green on the barrel plane, red on an enemy, orange = aim assist), cyan = muzzle,
    /// magenta = the level path the next bullet takes, to the weapon's range.
    /// </summary>
    public sealed class AimGizmos : MonoBehaviour
    {
        private const float c_PointRadius = 0.12f;
        private const float c_MuzzleRadius = 0.08f;

        [SerializeField] private PlayerWeapon m_Weapon;
        [SerializeField] private bool m_Show = true;

        private void OnDrawGizmos()
        {
            if (!m_Show || !Application.isPlaying || m_Weapon == null || m_Weapon.Muzzle == null)
                return;

            Vector3 muzzle = m_Weapon.Muzzle.position;
            AimSolution aim = m_Weapon.Aim;
            if (aim.IsValid)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(cam.transform.position, aim.Point);
                }
                Gizmos.color = aim.Source == AimSource.Enemy ? Color.red : aim.Source == AimSource.Assist ? new Color(1f, 0.55f, 0f) : Color.green;
                Gizmos.DrawSphere(aim.Point, c_PointRadius);
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(muzzle, c_MuzzleRadius);
            Gizmos.color = Color.magenta;
            float range = m_Weapon.Config != null ? m_Weapon.Config.Range : 1f;
            Gizmos.DrawLine(muzzle, muzzle + m_Weapon.ShotDirection * range);
        }
    }
}
