/**
 * DentalClinic Blog Editor & Media Upload Helper
 * Handles Quill editor setup, Image & Video uploads with XMLHttpRequest progress tracking and abort support.
 */

class BlogEditorManager {
    constructor(options) {
        this.quillContainerSelector = options.quillContainerSelector || '#quillEditorContainer';
        this.hiddenContentSelector = options.hiddenContentSelector || '#hiddenContent';
        this.uploadUrl = options.uploadUrl || '/Dentist/Blog/UploadMedia';
        this.blogPostId = options.blogPostId || null;
        this.activeXhr = null;

        this.initElements();
        this.initQuill();
        this.initCoverVideoPreview();
    }

    initElements() {
        // Create or get the upload progress modal / overlay
        let modalEl = document.getElementById('mediaUploadProgressModal');
        if (!modalEl) {
            const html = `
                <div class="modal fade" id="mediaUploadProgressModal" data-bs-backdrop="static" data-bs-keyboard="false" tabindex="-1" aria-hidden="true">
                    <div class="modal-dialog modal-dialog-centered">
                        <div class="modal-content border-0 shadow rounded-4 p-3">
                            <div class="modal-body text-center">
                                <div class="mb-3">
                                    <div class="spinner-border text-primary" role="status" id="uploadSpinner">
                                        <span class="visually-hidden">Đang tải...</span>
                                    </div>
                                </div>
                                <h6 class="fw-bold mb-1" id="uploadModalTitle">Đang tải tập tin lên đám mây...</h6>
                                <p class="text-muted small mb-3" id="uploadModalFileName">video.mp4</p>
                                
                                <div class="progress mb-2" style="height: 12px; border-radius: 6px;">
                                    <div id="uploadProgressBar" class="progress-bar progress-bar-striped progress-bar-animated bg-primary" 
                                         role="progressbar" style="width: 0%;" aria-valuenow="0" aria-valuemin="0" aria-valuemax="100"></div>
                                </div>
                                <div class="d-flex justify-content-between small text-muted mb-3">
                                    <span id="uploadProgressPercent">0%</span>
                                    <span id="uploadProgressSize">0 MB / 0 MB</span>
                                </div>
                                
                                <button type="button" class="btn btn-outline-danger btn-sm rounded-pill px-4" id="cancelUploadBtn">
                                    <i class="bi bi-x-circle me-1"></i> Hủy tải lên
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            `;
            document.body.insertAdjacentHTML('beforeend', html);
            modalEl = document.getElementById('mediaUploadProgressModal');
        }

        this.modal = new bootstrap.Modal(modalEl);
        this.progressBar = document.getElementById('uploadProgressBar');
        this.progressPercent = document.getElementById('uploadProgressPercent');
        this.progressSize = document.getElementById('uploadProgressSize');
        this.modalFileName = document.getElementById('uploadModalFileName');
        this.modalTitle = document.getElementById('uploadModalTitle');
        this.cancelBtn = document.getElementById('cancelUploadBtn');

        this.cancelBtn.addEventListener('click', () => {
            if (this.activeXhr) {
                this.activeXhr.abort();
                this.activeXhr = null;
                this.modal.hide();
                alert('Đã hủy tải lên tập tin.');
            }
        });
    }

    initQuill() {
        // Register custom video blot or custom handler
        const container = document.querySelector(this.quillContainerSelector);
        if (!container) return;

        this.quill = new Quill(this.quillContainerSelector, {
            theme: 'snow',
            placeholder: 'Nhập nội dung bài viết chi tiết...',
            modules: {
                toolbar: {
                    container: [
                        [{ 'header': [1, 2, 3, 4, false] }],
                        ['bold', 'italic', 'underline', 'strike'],
                        [{ 'color': [] }, { 'background': [] }],
                        [{ 'list': 'ordered' }, { 'list': 'bullet' }],
                        [{ 'align': [] }],
                        ['blockquote'],
                        ['link', 'image', 'video'],
                        ['clean']
                    ],
                    handlers: {
                        image: () => this.handleImageUpload(),
                        video: () => this.handleVideoUpload()
                    }
                }
            }
        });
    }

    handleImageUpload() {
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = '.jpg,.jpeg,.png,.webp';
        input.click();

        input.onchange = () => {
            const file = input.files[0];
            if (!file) return;

            // Validate extension & size (5 MB)
            const validExts = ['.jpg', '.jpeg', '.png', '.webp'];
            const fileExt = '.' + file.name.split('.').pop().toLowerCase();
            if (!validExts.includes(fileExt)) {
                alert('Định dạng ảnh không hợp lệ. Vui lòng chọn .jpg, .jpeg, .png hoặc .webp.');
                return;
            }
            if (file.size > 5 * 1024 * 1024) {
                alert('Dung lượng ảnh vượt quá 5 MB. Vui lòng chọn ảnh nhỏ hơn.');
                return;
            }

            this.performUpload(file, 'Image', (data) => {
                const range = this.quill.getSelection(true);
                this.quill.insertEmbed(range.index, 'image', data.url);
                this.quill.setSelection(range.index + 1);
            });
        };
    }

    handleVideoUpload() {
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = '.mp4,.webm';
        input.click();

        input.onchange = () => {
            const file = input.files[0];
            if (!file) return;

            // Validate extension & size (50 MB)
            const validExts = ['.mp4', '.webm'];
            const fileExt = '.' + file.name.split('.').pop().toLowerCase();
            if (!validExts.includes(fileExt)) {
                alert('Định dạng video không hợp lệ. Vui lòng chọn .mp4 hoặc .webm.');
                return;
            }
            if (file.size > 50 * 1024 * 1024) {
                alert('Dung lượng video vượt quá 50 MB. Vui lòng nén video hoặc chọn tập tin nhỏ hơn.');
                return;
            }

            this.performUpload(file, 'Video', (data) => {
                const range = this.quill.getSelection(true);
                const posterAttr = data.thumbnailUrl ? `poster="${data.thumbnailUrl}"` : '';
                const videoHtml = `<p><video controls preload="metadata" ${posterAttr} style="max-width: 100%; border-radius: 8px;"><source src="${data.url}" type="${file.type || 'video/mp4'}">Trình duyệt không hỗ trợ thẻ video.</video></p><p><br></p>`;
                this.quill.clipboard.dangerouslyPasteHTML(range.index, videoHtml);
            });
        };
    }

    performUpload(file, mediaType, onSuccess) {
        const formData = new FormData();
        formData.append('file', file);
        formData.append('mediaType', mediaType);
        if (this.blogPostId) {
            formData.append('blogPostId', this.blogPostId);
        }

        // Show Progress Modal
        this.modalTitle.textContent = mediaType === 'Video' ? 'Đang tải video lên máy chủ...' : 'Đang tải ảnh lên máy chủ...';
        this.modalFileName.textContent = `${file.name} (${(file.size / (1024 * 1024)).toFixed(2)} MB)`;
        this.progressBar.style.width = '0%';
        this.progressPercent.textContent = '0%';
        this.progressSize.textContent = `0 MB / ${(file.size / (1024 * 1024)).toFixed(2)} MB`;
        this.modal.show();

        const xhr = new XMLHttpRequest();
        this.activeXhr = xhr;

        xhr.upload.onprogress = (event) => {
            if (event.lengthComputable) {
                const percentComplete = Math.round((event.loaded / event.total) * 100);
                const loadedMB = (event.loaded / (1024 * 1024)).toFixed(2);
                const totalMB = (event.total / (1024 * 1024)).toFixed(2);

                this.progressBar.style.width = percentComplete + '%';
                this.progressPercent.textContent = percentComplete + '%';
                this.progressSize.textContent = `${loadedMB} MB / ${totalMB} MB`;
            }
        };

        xhr.onload = () => {
            this.activeXhr = null;
            this.modal.hide();

            if (xhr.status >= 200 && xhr.status < 300) {
                try {
                    const result = JSON.parse(xhr.responseText);
                    if (result.success && result.url) {
                        onSuccess(result);
                    } else {
                        alert(result.message || 'Lỗi xử lý tải tập tin.');
                    }
                } catch (e) {
                    alert('Lỗi định dạng phản hồi từ máy chủ.');
                }
            } else if (xhr.status === 413) {
                alert('Tập tin quá lớn (vượt quá giới hạn cho phép của máy chủ).');
            } else {
                try {
                    const errRes = JSON.parse(xhr.responseText);
                    alert(errRes.message || `Lỗi tải lên (${xhr.status}).`);
                } catch (e) {
                    alert(`Tải tập tin thất bại (Mã lỗi ${xhr.status}).`);
                }
            }
        };

        xhr.onerror = () => {
            this.activeXhr = null;
            this.modal.hide();
            alert('Không thể kết nối đến máy chủ để tải tập tin.');
        };

        xhr.onabort = () => {
            this.activeXhr = null;
        };

        xhr.open('POST', this.uploadUrl, true);
        xhr.send(formData);
    }

    uploadCoverVideo(file, targetInput, videoPreviewEl) {
        if (!file) return;

        const validExts = ['.mp4', '.webm'];
        const fileExt = '.' + file.name.split('.').pop().toLowerCase();
        if (!validExts.includes(fileExt)) {
            alert('Định dạng video bìa không hợp lệ. Vui lòng chọn .mp4 hoặc .webm.');
            return;
        }
        if (file.size > 50 * 1024 * 1024) {
            alert('Dung lượng video vượt quá 50 MB.');
            return;
        }

        this.performUpload(file, 'Video', (data) => {
            targetInput.value = data.url;
            if (videoPreviewEl) {
                videoPreviewEl.src = data.url;
                if (data.thumbnailUrl) videoPreviewEl.poster = data.thumbnailUrl;
                videoPreviewEl.style.display = 'block';
            }
        });
    }

    initCoverVideoPreview() {
        const videoInput = document.getElementById('coverVideoUrlInput');
        const videoPreview = document.getElementById('coverVideoPreview');
        const uploadVideoBtn = document.getElementById('uploadCoverVideoBtn');
        const fileInput = document.getElementById('coverVideoFileInput');

        if (videoInput && videoPreview) {
            videoInput.addEventListener('input', () => {
                const url = videoInput.value.trim();
                if (url) {
                    videoPreview.src = url;
                    videoPreview.style.display = 'block';
                } else {
                    videoPreview.style.display = 'none';
                }
            });
        }

        if (uploadVideoBtn && fileInput && videoInput) {
            uploadVideoBtn.addEventListener('click', () => {
                fileInput.click();
            });

            fileInput.addEventListener('change', () => {
                const file = fileInput.files[0];
                if (file) {
                    this.uploadCoverVideo(file, videoInput, videoPreview);
                }
            });
        }
    }

    syncContent() {
        const hiddenEl = document.querySelector(this.hiddenContentSelector);
        if (hiddenEl && this.quill) {
            hiddenEl.value = this.quill.root.innerHTML;
            return hiddenEl.value;
        }
        return '';
    }
}

window.BlogEditorManager = BlogEditorManager;
