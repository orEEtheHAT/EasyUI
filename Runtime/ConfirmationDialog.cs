using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace EasyUI
{
    public enum ConfirmationResult { Confirmed, Cancelled }

    [RequireComponent(typeof(UIWindow)), AddComponentMenu("EasyUI/Confirmation Dialog")]
    public sealed class ConfirmationDialog : MonoBehaviour
    {
        public Text Message;
        public Button ConfirmButton;
        public Button CancelButton;
        public ConfirmationResult? Result { get; private set; }
        public event Action<ConfirmationResult> Completed;
        private TaskCompletionSource<ConfirmationResult> completion;
        private UIWindow window;
        private void Awake() { window = GetComponent<UIWindow>(); }
        private void OnEnable()
        {
            if (!window) window = GetComponent<UIWindow>();
            window.Closed += OnClosed;
            if (ConfirmButton) ConfirmButton.onClick.AddListener(Confirm);
            if (CancelButton) CancelButton.onClick.AddListener(Cancel);
        }
        private void OnDisable()
        {
            if (ConfirmButton) ConfirmButton.onClick.RemoveListener(Confirm);
            if (CancelButton) CancelButton.onClick.RemoveListener(Cancel);
            if (window) window.Closed -= OnClosed;
            Complete(Result ?? ConfirmationResult.Cancelled);
        }
        public Task<ConfirmationResult> Ask(string message)
        {
            if (completion != null && !completion.Task.IsCompleted) return completion.Task;
            Result = null;
            if (Message) Message.text = message;
            completion = new TaskCompletionSource<ConfirmationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            return completion.Task;
        }
        public void Confirm() { Complete(ConfirmationResult.Confirmed); window.Close(); }
        public void Cancel() { Complete(ConfirmationResult.Cancelled); window.Close(); }
        private void OnClosed(WindowCloseReason reason) => Complete(Result ?? ConfirmationResult.Cancelled);
        private void Complete(ConfirmationResult result)
        {
            if (Result.HasValue) return;
            Result = result;
            completion?.TrySetResult(result);
            Signals.Invoke(Completed, result);
        }
    }
}
