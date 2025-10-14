// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

function toggleFilter(){
    // placeholder: implement filter UI toggle
    alert('Filter clicked');
}
function toggleFavorites(btn){
    var svg = btn.querySelector('.heart-icon');
    var isFav = btn.classList.toggle('fav');
    if(isFav){
        svg.style.fill = getComputedStyle(document.documentElement).getPropertyValue('--accent') || '#ef4444';
        svg.style.stroke = 'none';
    } else {
        svg.style.fill = 'none';
        svg.style.stroke = 'currentColor';
    }
}
function toggleSidebar(){
    var sidebar = document.querySelector('.sidebar');
    var toggleBtn = document.querySelector('.sidebar-toggle');
    var expanded = !sidebar.classList.contains('collapsed');
    if(expanded){
        sidebar.classList.add('collapsed');
        toggleBtn.setAttribute('aria-label','Expand sidebar');
        toggleBtn.setAttribute('title','Expand sidebar');
    } else {
        sidebar.classList.remove('collapsed');
        toggleBtn.setAttribute('aria-label','Collapse sidebar');
        toggleBtn.setAttribute('title','Collapse sidebar');
    }
}
