const chips = document.querySelectorAll('[data-chip]');
const navItems = document.querySelectorAll('[data-nav]');

chips.forEach((chip) => {
  chip.addEventListener('click', () => {
    chips.forEach((btn) => btn.classList.remove('chip-active'));
    chip.classList.add('chip-active');
  });
});

navItems.forEach((item) => {
  item.addEventListener('click', () => {
    navItems.forEach((btn) => btn.classList.remove('nav-active'));
    item.classList.add('nav-active');
  });
});
