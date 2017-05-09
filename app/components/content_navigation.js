zuix.controller(function (cp) {
    var menuOpen = true;
    var smallScreen = false;
    var firstCheck = true;
    var overlay = null;

    cp.init = function () {
        this.options().html = false;
        //this.options().css = false;
    };

    cp.create = function () {
        window.addEventListener("resize", function () {
            sizeCheck();
        });
        zuix.field('toggle-menu').on('click', function () {
            if (menuOpen)
                closeMenu();
            else
                openMenu();
        });
        overlay = zuix.$(document.createElement('div'));
        overlay.css({
            'position': 'absolute',
            'top': '64px',
            'left': 0,
            'bottom': 0,
            'right': 0,
            'z-index': 10,
            'background-color': 'rgba(0, 0, 0, 0.35)'
        }).on('click', function () {
            closeMenu();
        }).hide();
        zuix.field('side-menu')
            .css('z-index', 15)
            .parent().append(overlay.get());
        sizeCheck();
        firstCheck = false;
    };

    function sizeCheck() {
        var width = document.body.clientWidth;
        if (width < 960) {
            if (!smallScreen || firstCheck) {
                smallScreen = true;
                zuix.$('main').css('left', '0');
                zuix.field('header-title').css('margin-left', '0');
                zuix.field('toggle-menu').show()
                    .animateCss('bounceInLeft', {duration: '0.2s'});
                setTimeout(closeMenu, 1000);
            }
        } else {
            if (smallScreen || firstCheck) {
                if (smallScreen)
                    overlay.hide();
                smallScreen = false;
                zuix.$('main').css('left', '250px');
                zuix.field('header-title').css('margin-left', '250px');
                zuix.field('toggle-menu').hide();
                openMenu();
            }
        }
    }

    function openMenu() {
        zuix.field('side-menu').show();
        if (!menuOpen) {
            menuOpen = true;
            zuix.field('side-menu').animateCss('slideInLeft', { delay: '0.1s', duration: '0.3s' });
            cp.trigger('menu_open');
            if (smallScreen) {
                overlay.show().animateCss('fadeIn');
                zuix.field('side-menu').find('a').one('click', function() {
                    closeMenu();
                });
            }
        }
    }

    function closeMenu() {
        if (menuOpen && smallScreen) {
            menuOpen = false;
            zuix.field('side-menu').animateCss('slideOutLeft', { delay: '0.1s', duration: '0.3s' }, function () {
                if (!menuOpen) {
                    this.hide();
                }
            });
            if (smallScreen) {
                overlay.hide();
            }
            cp.trigger('menu_close');
        }
    }

});
