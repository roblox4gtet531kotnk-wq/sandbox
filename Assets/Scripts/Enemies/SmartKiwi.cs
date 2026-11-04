using UnityEngine;

namespace KiviGun
{
    /// <summary>
    /// Общая логика восприятия киви: «видит ли птица цель».
    ///
    /// ВАЖНО: здесь НЕТ зависимости от UnityEngine.AI (NavMeshAgent).
    /// Раньше в этом же файле жил компонент «умная киви на навмеше»,
    /// и из-за него весь проект не компилировался:
    ///   CS1069: The type name 'NavMeshAgent' could not be found in the
    ///   namespace 'UnityEngine.AI' (модуль com.unity.modules.ai не был
    ///   прописан в Packages/manifest.json).
    ///
    /// Теперь: логика зрения — здесь (её использует KiwiEnemy),
    /// а вариант с навмешем лежит отдельным файлом SmartKiwiNavMesh.cs
    /// и включается только если самому понадобится настоящая навигация
    /// (см. инструкцию в том файле).
    /// </summary>
    public static class SmartKiwi
    {
        private static readonly RaycastHit[] SightBuffer = new RaycastHit[24];

        /// <summary>
        /// Видит ли «наблюдатель» цель: есть ли она в радиусе, в секторе обзора
        /// и не закрыта ли стеной.
        ///
        /// Исправления (были ошибки):
        ///   1) луч идёт ТОЧНО ДО ЦЕЛИ, а не на всю дальность — иначе стена
        ///      ЗА игроком закрывала обзор;
        ///   2) свои коллайдеры проверяются по иерархии (transform и его дети),
        ///      а не по общему root: у птицы и игрока root может совпадать
        ///      (например, общий корень уровня) — тогда чужие стены
        ///      игнорировались бы;
        ///   3) другие птицы не считаются препятствием;
        ///   4) если буфер попаданий переполнен — считаем, что обзор закрыт
        ///      (лучше «не видит», чем «видит сквозь стену»).
        /// </summary>
        public static bool CanSee(Transform observer, Transform target, float range, float angle, int mask)
        {
            if (observer == null || target == null) return false;

            Vector3 eye = observer.position + Vector3.up * 0.8f;
            Vector3 aim = target.position + Vector3.up * 0.8f;
            Vector3 direction = aim - eye;

            float distance = direction.magnitude;
            if (distance > range) return false;
            if (distance < 0.01f) return true;

            Vector3 flat = direction;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.001f &&
                Vector3.Angle(observer.forward, flat) > angle * 0.5f)
            {
                return false;
            }

            Vector3 dir = direction / distance;

            // До цели, а не на всю range: стена за игроком не должна мешать.
            int count = Physics.RaycastNonAlloc(eye, dir, SightBuffer, distance, mask,
                                                QueryTriggerInteraction.Ignore);

            bool overflow = count >= SightBuffer.Length;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = SightBuffer[i];
                if (hit.collider == null) continue;

                Transform t = hit.collider.transform;

                // Свои коллайдеры (птица) и коллайдер цели (игрок) — не помеха.
                if (IsSelfOrChild(t, observer)) continue;
                if (IsSelfOrChild(t, target)) continue;

                // Соседние птицы тоже не мешают видеть игрока.
                if (t.GetComponentInParent<KiwiEnemy>() != null) continue;

                // Всё остальное (стены, ящики, машины) — помеха.
                return false;
            }

            return !overflow;
        }

        /// <summary>Сам объект или его потомок?</summary>
        public static bool IsSelfOrChild(Transform candidate, Transform root)
        {
            if (candidate == null || root == null) return false;
            if (candidate == root) return true;
            return candidate.IsChildOf(root);
        }
    }
}
