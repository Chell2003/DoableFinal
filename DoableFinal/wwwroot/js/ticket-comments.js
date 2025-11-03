function editComment(commentId, commentText) {
    // Hide content and show edit form
    $(`.comment-content-${commentId}`).hide();
    $(`.comment-edit-form-${commentId}`).show();
}

function cancelEdit(commentId) {
    // Hide edit form and show content
    $(`.comment-edit-form-${commentId}`).hide();
    $(`.comment-content-${commentId}`).show();
}