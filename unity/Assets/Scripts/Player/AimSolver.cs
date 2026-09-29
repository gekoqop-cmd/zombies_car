using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Props;

namespace ZombiCar.Player
{
    /// <summary>What the aim point was taken from.</summary>
    public enum AimSource
    {
        None,
        Plane,    // cursor ray x horizontal plane at barrel height
        Enemy,    // cursor ray hit an enemy collider
        Assist,   // plane point snapped to a nearby enemy's centre
    }

    public struct AimSolution
    {
        public AimSource Source;
        public Vector3 Point;
        public Collider Target;

        public bool IsValid => Source != AimSource.None;
    }

    /// <summary>
    /// Turns a camera ray through the cursor into the point the gun should hit. The cursor is never projected onto
    /// the ground: the camera looks down at ~50°, so a ground point lies well behind whatever is drawn under the
    /// cursor and a level shot at barrel height would pass beside it.
    /// </summary>
    public static class AimSolver
    {
        private const float c_MaxRayDistance = 200f;
        private const int c_MaxAssistCandidates = 8;

        private static readonly Collider[] s_Candidates = new Collider[c_MaxAssistCandidates];

        public static AimSolution FromRay(Ray ray, float muzzleHeight, WeaponConfig config)
        {
            // 1. Enemy under the cursor: the ray only picks the enemy (it meets the capsule on its camera-facing edge);
            //    the shot goes to the body's axis at barrel height, so it hits the middle, not the rim.
            if (Physics.SphereCast(ray, config.CursorPickRadius, out RaycastHit hit, c_MaxRayDistance, Layers.Enemies,
                    QueryTriggerInteraction.Ignore))
                return new AimSolution { Source = AimSource.Enemy, Point = AtBarrel(hit.collider, muzzleHeight), Target = hit.collider };

            // 2. Otherwise the ray meets the horizontal plane the bullets fly in.
            var plane = new Plane(Vector3.up, new Vector3(0f, muzzleHeight, 0f));
            if (!plane.Raycast(ray, out float distance))
                return default;
            Vector3 point = ray.GetPoint(distance);

            // 3. Aim assist: a near miss is pulled to the closest enemy's centre.
            if (config.AimAssist && config.AimAssistRadius > 0f)
            {
                int count = Physics.OverlapSphereNonAlloc(point, config.AimAssistRadius, s_Candidates, Layers.Enemies,
                    QueryTriggerInteraction.Ignore);
                Collider best = null;
                float bestSqr = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    Vector3 offset = s_Candidates[i].bounds.center - point;
                    offset.y = 0f;
                    if (offset.sqrMagnitude < bestSqr)
                    {
                        bestSqr = offset.sqrMagnitude;
                        best = s_Candidates[i];
                    }
                }
                if (best != null)
                    return new AimSolution { Source = AimSource.Assist, Point = AtBarrel(best, muzzleHeight), Target = best };

                // No zombie near the cursor: a cursor on (or next to) an explosive barrel aims at its middle.
                ExplosiveProp barrel = NearestExplosive(point, config.AimAssistRadius + c_ExplosiveAssistExtra);
                if (barrel != null)
                {
                    Vector3 c = barrel.transform.position;
                    return new AimSolution { Source = AimSource.Assist, Point = new Vector3(c.x, muzzleHeight, c.z) };
                }
            }
            return new AimSolution { Source = AimSource.Plane, Point = point };
        }

        // Barrels are drawn tall on screen: the cursor over the top of one meets the bullet plane well behind its base.
        private const float c_ExplosiveAssistExtra = 0.4f;

        private static ExplosiveProp NearestExplosive(Vector3 point, float radius)
        {
            ExplosiveProp best = null;
            float bestSqr = radius * radius;
            foreach (ExplosiveProp prop in ExplosiveProp.All)
            {
                if (!prop.IsAlive)
                    continue;
                Vector3 d = prop.transform.position - point;
                d.y = 0f;
                if (d.sqrMagnitude < bestSqr)
                {
                    bestSqr = d.sqrMagnitude;
                    best = prop;
                }
            }
            return best;
        }

        private static Vector3 AtBarrel(Collider enemy, float muzzleHeight)
        {
            Vector3 center = enemy.bounds.center;
            return new Vector3(center.x, muzzleHeight, center.z);
        }

        /// <summary>Level (y = 0) unit direction from <paramref name="from"/> to <paramref name="to"/>; false when they are too close.</summary>
        public static bool LevelDirection(Vector3 from, Vector3 to, float minDistance, out Vector3 direction)
        {
            direction = to - from;
            direction.y = 0f;
            if (direction.sqrMagnitude < minDistance * minDistance)
                return false;
            direction.Normalize();
            return true;
        }
    }
}
