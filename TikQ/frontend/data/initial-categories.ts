export const initialCategoriesData = {
  hardware: {
    label: "مشکلات سخت‌افزاری",
    description: "مشکلات مربوط به تجهیزات فیزیکی",
    subIssues: {
      "computer-not-working": {
        label: "رایانه کار نمی‌کند",
        description: "رایانه روشن نمی‌شود یا به درستی کار نمی‌کند",
      },
      "printer-issues": {
        label: "مشکلات چاپگر",
        description: "چاپگر کار نمی‌کند یا کیفیت چاپ مناسب نیست",
      },
      "monitor-problems": {
        label: "مشکلات مانیتور",
        description: "مانیتور تصویر نمایش نمی‌دهد یا مشکل در نمایش دارد",
      },
    },
  },
  software: {
    label: "مشکلات نرم‌افزاری",
    description: "مشکلات مربوط به نرم‌افزارها و سیستم عامل",
    subIssues: {
      "os-issues": {
        label: "مشکلات سیستم عامل",
        description: "مشکلات ویندوز، مک یا لینوکس",
      },
      "application-problems": {
        label: "مشکلات نرم‌افزار",
        description: "نرم‌افزار کار نمی‌کند یا خطا می‌دهد",
      },
      "software-installation": {
        label: "نصب نرم‌افزار",
        description: "نیاز به نصب یا به‌روزرسانی نرم‌افزار",
      },
    },
  },
  network: {
    label: "مشکلات شبکه",
    description: "مشکلات اتصال به اینترنت و شبکه",
    subIssues: {
      "internet-connection": {
        label: "مشکل اتصال اینترنت",
        description: "عدم دسترسی به اینترنت یا اتصال کند",
      },
      "wifi-problems": {
        label: "مشکلات وای‌فای",
        description: "عدم اتصال به شبکه بی‌سیم",
      },
      "network-drive": {
        label: "دسترسی به درایو شبکه",
        description: "عدم دسترسی به فولدرهای مشترک",
      },
    },
  },
  email: {
    label: "مشکلات ایمیل",
    description: "مشکلات مربوط به ایمیل و پیام‌رسانی",
    subIssues: {
      "email-not-working": {
        label: "ایمیل کار نمی‌کند",
        description: "عدم دریافت یا ارسال ایمیل",
      },
      "email-setup": {
        label: "تنظیم ایمیل",
        description: "نیاز به تنظیم حساب ایمیل جدید",
      },
      "email-sync": {
        label: "همگام‌سازی ایمیل",
        description: "مشکل در همگام‌سازی ایمیل‌ها",
      },
    },
  },
  security: {
    label: "مسائل امنیتی",
    description: "مشکلات امنیتی و حفاظت از اطلاعات",
    subIssues: {
      "virus-malware": {
        label: "ویروس یا بدافزار",
        description: "احتمال آلودگی سیستم به ویروس",
      },
      "password-reset": {
        label: "بازنشانی رمز عبور",
        description: "فراموشی رمز عبور حساب کاربری",
      },
      "security-incident": {
        label: "حادثه امنیتی",
        description: "مشکوک بودن فعالیت‌های غیرعادی",
      },
    },
  },
  access: {
    label: "درخواست دسترسی",
    description: "درخواست دسترسی به سیستم‌ها و منابع",
    subIssues: {
      "system-access": {
        label: "دسترسی به سیستم",
        description: "نیاز به دسترسی به سیستم یا نرم‌افزار خاص",
      },
      "permission-change": {
        label: "تغییر سطح دسترسی",
        description: "نیاز به تغییر مجوزهای کاربری",
      },
      "new-account": {
        label: "حساب کاربری جدید",
        description: "درخواست ایجاد حساب کاربری جدید",
      },
    },
  },
}