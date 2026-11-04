using UnityEngine;

namespace KiviGun
{
    /// <summary>
    /// Показывает всплывашку, когда получено достижение.
    ///
    /// ВАЖНО: компонент должен быть на ОДНОМ объекте, который живёт во время игры
    /// (в проекте это объект Bootstrap).
    ///
    /// Обработчик — НЕ статический: у каждого экземпляра своя подписка, и
    /// OnDisable снимает ровно свою. Раньше обработчик был статическим, и
    /// отключение одного экземпляра снимало подписку другого (уведомления
    /// пропадали).
    /// </summary>
    public sealed class AchievementNotifications : MonoBehaviour
    {
        private void OnEnable()
        {
            AchievementSystem.Unlocked += ShowAchievement;
        }

        private void OnDisable()
        {
            AchievementSystem.Unlocked -= ShowAchievement;
        }

        private void ShowAchievement(string title)
        {
            if (string.IsNullOrEmpty(title)) return;
            ToastManager.GetOrCreate().Show("★ ДОСТИЖЕНИЕ: " + title);
        }
    }
}
