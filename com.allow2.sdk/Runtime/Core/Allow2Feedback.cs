// Allow2 Unity SDK v2
// Copyright (c) 2026 Allow2 Pty Ltd. All rights reserved.

using System;

namespace Allow2
{
    /// <summary>
    /// Feedback submission manager.
    /// Allows children and parents to report bugs, request features,
    /// or report bypass attempts.
    ///
    /// API calls are driven by the bridge (coroutine). This class
    /// validates inputs and interprets responses.
    /// </summary>
    public class Allow2Feedback
    {
        /// <summary>Valid feedback categories.</summary>
        public static readonly string[] ValidCategories = new string[]
        {
            "bypass",
            "missing_feature",
            "not_working",
            "question",
            "other"
        };

        /// <summary>Fired when feedback is submitted successfully.</summary>
        public event Action<string, string> OnFeedbackSubmitted; // discussionId, category

        /// <summary>Fired on feedback error.</summary>
        public event Action<string> OnFeedbackError;

        /// <summary>Fired when feedback discussions are loaded.</summary>
        public event Action<Allow2ApiResponse> OnFeedbackLoaded;

        /// <summary>Fired when a reply is sent.</summary>
        public event Action<string, string> OnFeedbackReplySent; // discussionId, messageId

        /// <summary>
        /// Validate a feedback category string.
        /// </summary>
        public static bool IsValidCategory(string category)
        {
            if (string.IsNullOrEmpty(category)) return false;
            for (int i = 0; i < ValidCategories.Length; i++)
            {
                if (ValidCategories[i] == category) return true;
            }
            return false;
        }

        /// <summary>
        /// Validate feedback parameters before submission.
        /// Returns null if valid, or an error message string.
        /// </summary>
        public string ValidateSubmission(string category, string message)
        {
            if (string.IsNullOrEmpty(category))
            {
                return "Category is required";
            }
            if (!IsValidCategory(category))
            {
                return "Invalid category. Must be one of: bypass, missing_feature, not_working, question, other";
            }
            if (string.IsNullOrEmpty(message))
            {
                return "Message is required";
            }
            return null;
        }

        /// <summary>
        /// Handle the API response from submitFeedback.
        /// </summary>
        public void HandleSubmitResponse(Allow2ApiResponse response, string category)
        {
            if (response == null || !response.IsSuccess)
            {
                string error = response != null ? response.ErrorMessage : "Feedback submission failed";
                if (OnFeedbackError != null) OnFeedbackError(error);
                return;
            }

            string discussionId = response.GetString("discussionId");
            if (OnFeedbackSubmitted != null)
            {
                OnFeedbackSubmitted(discussionId, category);
            }
        }

        /// <summary>
        /// Handle the API response from loadFeedback.
        /// </summary>
        public void HandleLoadResponse(Allow2ApiResponse response)
        {
            if (response == null || !response.IsSuccess)
            {
                string error = response != null ? response.ErrorMessage : "Failed to load feedback";
                if (OnFeedbackError != null) OnFeedbackError(error);
                return;
            }

            if (OnFeedbackLoaded != null)
            {
                OnFeedbackLoaded(response);
            }
        }

        /// <summary>
        /// Handle the API response from feedbackReply.
        /// </summary>
        public void HandleReplyResponse(Allow2ApiResponse response, string discussionId)
        {
            if (response == null || !response.IsSuccess)
            {
                string error = response != null ? response.ErrorMessage : "Failed to send reply";
                if (OnFeedbackError != null) OnFeedbackError(error);
                return;
            }

            string messageId = response.GetString("messageId");
            if (OnFeedbackReplySent != null)
            {
                OnFeedbackReplySent(discussionId, messageId);
            }
        }

        /// <summary>
        /// Convert a feedback category to a human-readable label.
        /// </summary>
        public static string CategoryToLabel(string category)
        {
            if (category == "bypass") return "Bypass / Circumvention report";
            if (category == "missing_feature") return "Missing Feature report";
            if (category == "not_working") return "Not Working report";
            if (category == "question") return "Question";
            if (category == "other") return "General feedback";
            return "Feedback";
        }
    }
}
