zuix.controller(function (cp) {
    var menuOpen = true;
    var smallScreen = false;
    var firstCheck = true;

    var overlay = null;
    var menuButton = null;
    var sideMenu = null;
    var headerTitle = null;

    var smallScreenOffset = 960;

    cp.init = function () {
        this.options().html = false;
        this.options().css = false;
    };

    cp.create = function() {
        var fields = cp.options().controlFields;
        menuButton = zuix.field(fields.menuButton);
        sideMenu = zuix.field(fields.sideMenu);
        headerTitle = zuix.field(fields.headerTitle);
        // listen to window resize event to make layout responsive
        window.addEventListener('resize', function () {
            sizeCheck();
        });
        // toggle button click event to open/close menu
        menuButton.on('click', function () {
            if (menuOpen)
                closeMenu();
            else
                openMenu();
        });
        // add overlay for small screens when menu is open
        overlay = zuix.$(document.createElement('div'));
        overlay.css({
            'position': 'absolute',
            'top': sideMenu.position().y+'px',
            'left': 0,
            'bottom': 0,
            'right': 0,
            'z-index': 10,
            'background-color': 'rgba(0, 0, 0, 0.35)'
        }).on('click', function () {
            closeMenu();
        }).hide();
        sideMenu.css('z-index', 15)
            .parent().append(overlay.get());
        // detect screen size and set large/small layout
        sizeCheck();
        firstCheck = false;
    };

    function sizeCheck() {
        var width = document.body.clientWidth;
        if (width < smallScreenOffset) {
            if (!smallScreen || firstCheck) {
                smallScreen = true;
                zuix.$('main').css('left', '0');
                headerTitle.css('margin-left', '0');
                menuButton.show();
                setTimeout(closeMenu, 1000);
            }
        } else {
            if (smallScreen || firstCheck) {
                if (smallScreen)
                    overlay.hide();
                smallScreen = false;
                zuix.$('main').css('left', '250px');
                headerTitle.css('margin-left', '250px');
                menuButton.hide();
                openMenu();
            }
        }
    }

    function openMenu() {
        sideMenu.show();
        if (!menuOpen) {
            menuOpen = true;
            sideMenu.animateCss('slideInLeft', { delay: '0.1s', duration: '0.3s' });
            cp.trigger('menu_open');
            if (smallScreen) {
                overlay.show().animateCss('fadeIn');
                sideMenu.find('a').one('click', function() {
                    closeMenu();
                });
            }
            menuButton.animateCss('rotateOut', { duration: '0.5s' }, function () {
                this.find('i').html('arrow_back');
            });
        }
    }

    function closeMenu() {
        if (menuOpen && smallScreen) {
            menuOpen = false;
            sideMenu.animateCss('slideOutLeft', { delay: '0.1s', duration: '0.3s' }, function () {
                if (!menuOpen) {
                    this.hide();
                }
            });
            if (smallScreen) {
                overlay.hide();
            }
            menuButton.animateCss('rotateOut', { duration: '0.5s' }, function () {
                this.find('i').html('menu');
            });
            cp.trigger('menu_close');
        }
    }

});
