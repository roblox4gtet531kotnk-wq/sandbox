using System;
using System.Collections.Generic;
using UnityEngine;

namespace KiviGun
{
    /// <summary>
    /// Кнопка «Назад»: Escape на компьютере и системная кнопка «Назад» на Android.
    ///
    /// Без этого на телефоне кнопка «Назад» просто закрывала приложение
    /// (из магазина, из выбора уровня, из настроек), а на компьютере Escape
    /// не открывал меню паузы.
    ///
    /// Экраны складывают обработчики в стек: самый верхний вызывается
    /// и удаляется. Если стек пуст — ничего не делаем (приложение ведёт себя
    /// как обычно).
    /// </summary>
    public static class BackNavigator
    {
        private static readonly List<Action> _handlers = new List<Action>();

        /// <summary>Добавить обработчик «назад» (обычно вместе с открытием экрана).</summary>
        public static void Push(Action action)
        {
            if (action == null) return;
            // Не добавляем тот же обработчик дважды подряд.
            if (_handlers.Count > 0 && _handlers[_handlers.Count - 1] == action) return;
            _handlers.Add(action);
        }

        /// <summary>Убрать последний обработчик (экран закрылся).</summary>
        public static void Pop()
        {
            if (_handlers.Count > 0) _handlers.RemoveAt(_handlers.Count - 1);
        }

        /// <summary>Убрать все обработчики (смена сцены).</summary>
        public static void Clear()
        {
            _handlers.Clear();
        }

        /// <summary>Обработать нажатие «назад». true — обработчик был.</summary>
        public static bool Handle()
        {
            if (_handlers.Count == 0) return false;
            Action action = _handlers[_handlers.Count - 1];
            _handlers.RemoveAt(_handlers.Count - 1);
            try
            {
                if (action != null) action();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KiviGun] Ошибка обработчика «назад»: " + e.Message);
            }
            return true;
        }

        public static int Count => _handlers.Count;
    }

    /// <summary>
    /// Слушает Escape / системную кнопку «Назад» и передаёт нажатие
    /// в <see cref="BackNavigator"/>. Вешается на объект Bootstrap.
    /// </summary>
    public sealed class BackHandler : MonoBehaviour
    {
        private void Update()
        {
            // На Android аппаратная кнопка «Назад» приходит как Escape.
            if (InputHub.KeyDown(KeyCode.Escape)) BackNavigator.Handle();
        }
    }
}
