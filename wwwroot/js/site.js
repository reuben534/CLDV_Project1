// Simulation of live data updates for high-fidelity feel
document.addEventListener('DOMContentLoaded', function () {
    const kpiValues = document.querySelectorAll('.kpi-card h3');
    
    if (kpiValues.length > 0) {
        setInterval(() => {
            kpiValues.forEach(kpi => {
                const currentText = kpi.innerText;
                if (currentText.includes('%')) {
                    // System Uptime or Error Rate
                    let val = parseFloat(currentText);
                    let change = (Math.random() * 0.02 - 0.01);
                    kpi.innerText = (val + change).toFixed(2) + '%';
                } else if (currentText.length < 5) {
                    // Active Orders or Queue Depth
                    let val = parseInt(currentText);
                    let change = Math.floor(Math.random() * 3) - 1; // -1, 0, or 1
                    if (val + change > 0) {
                        kpi.innerText = val + change;
                    }
                }
            });
        }, 3000);
    }

    // Add visual feedback to buttons
    const buttons = document.querySelectorAll('.btn');
    buttons.forEach(btn => {
        btn.addEventListener('click', function() {
            if (!this.classList.contains('btn-login')) {
                const originalText = this.innerHTML;
                this.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Processing...';
                setTimeout(() => {
                    this.innerHTML = originalText;
                }, 1000);
            }
        });
    });

    // Auto-hide alerts after 1 second
    const alerts = document.querySelectorAll('.alert');
    alerts.forEach(alert => {
        setTimeout(() => {
            // Using Bootstrap 5's Alert API to close the alert
            if (typeof bootstrap !== 'undefined' && bootstrap.Alert) {
                const bsAlert = bootstrap.Alert.getOrCreateInstance(alert);
                if (bsAlert) {
                    bsAlert.close();
                }
            } else {
                // Fallback for direct DOM manipulation
                alert.classList.remove('show');
                setTimeout(() => alert.remove(), 150);
            }
        }, 1000);
    });
});

// Global function for SweetAlert2 delete confirmation
function confirmDelete(event, itemName) {
    event.preventDefault();
    const form = event.target.closest('form');
    
    Swal.fire({
        title: 'Are you sure?',
        text: `You are about to permanently delete "${itemName}". This action cannot be undone!`,
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#dc3545',
        cancelButtonColor: '#6c757d',
        confirmButtonText: '<i class="fas fa-trash-alt me-1"></i> Yes, delete it!',
        cancelButtonText: 'Cancel',
        reverseButtons: true,
        focusCancel: true,
        background: '#fff',
        borderRadius: '10px'
    }).then((result) => {
        if (result.isConfirmed) {
            // Show loading state
            Swal.fire({
                title: 'Deleting...',
                text: 'Please wait while we remove the asset from Azure Storage.',
                allowOutsideClick: false,
                didOpen: () => {
                    Swal.showLoading();
                }
            });
            form.submit();
        }
    });
    return false;
}
