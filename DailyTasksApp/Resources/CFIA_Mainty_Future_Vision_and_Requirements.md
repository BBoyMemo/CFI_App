# CFIA / Mainty — Future Application Vision & Requirements

## 1. Purpose

Този документ е отделен от техническия handoff на съществуващия Mainty проект.

Той описва новата посока на приложението за **County Food Ingredients**.

Важно:
- Името още не е окончателно. Една от идеите е **CFIA**.
- Приложението е специално за County Food Ingredients, не универсален продукт за други фирми.
- Съществуващият Mainty проект е техническата основа, но приложението ще бъде значително разширено.
- Този документ описва идеи и изисквания, а не окончателна техническа спецификация.
- GitHub repository е source of truth за текущия код.

---

# 2. Основна визия

Mainty започва като Maintenance приложение, но бъдещата посока е да се превърне във вътрешна фирмена платформа за County Food Ingredients.

Основните вече обсъдени области са:

1. Maintenance
2. Авариите / Breakdowns / Work Orders
3. Планиране на смените
4. Виждане на смените от работниците
5. Login / Logout
6. Автоматично Clock In / Clock Out
7. Ръчно Clock In / Clock Out при проблем
8. Локална/offline работа и синхронизация
9. Фирмени съобщения
10. Push notifications
11. Таргетирани съобщения към конкретни служители
12. Съобщения към Department
13. Web версия
14. Android приложение
15. iPhone/iOS приложение

След Version 1:
16. Incident Reporting — отворен въпрос за обсъждане.
17. Производство, продукти, количества, доставки, склад и подобни — да се обсъдят с реалните отдели.

---

# 3. Име

Името още не е решено.

Временна идея:

**CFIA**

То е свързано с County Food Ingredients, но не трябва да се приема като окончателно име.

Докато не се реши името, може да се използва „CFIA / Mainty“.

---

# 4. Основен принцип

Приложението трябва да бъде:
- лесно за използване;
- бързо;
- удобно за хора без технически знания;
- удобно на компютър и телефон;
- способно да работи при временна липса на мрежа;
- способно да синхронизира данните след възстановяване на връзката;
- сигурно;
- базирано на роли и permissions;
- съобразено с реалната работа на County Food Ingredients.

Не трябва да се добавят функции само защото технически могат да бъдат направени.

---

# 5. Maintenance

Maintenance остава основен модул.

Съществуващият Mainty има концепции като:
- Breakdown
- Work Order
- Engineer
- Engineering Manager
- Operator
- QA
- Production Manager
- Dashboard
- Shift Tasks
- PPM

Този модул ще бъде развит допълнително.

Важно: не са одобрени автоматично QR scanning, voice description, Start/Finish Job, инженерски чеклисти и подобни идеи. Те не са част от фиксирания scope, освен ако по-късно не бъдат одобрени.

Основният въпрос остава: как да направим Maintenance workflow максимално лесен за инженерите.

---

# 6. Shift Planning / Планиране на смените

Това е един от основните нови модули.

Production Manager или друг оторизиран мениджър трябва да може да:
- създава смени;
- планира работници;
- вижда графика;
- променя графика;
- планира следващата седмица;
- планира няколко седмици напред;
- при нужда да планира приблизително месец напред.

Работниците трябва да могат да виждат:
- кога са на работа;
- коя смяна имат;
- датата;
- часа;
- следващите си смени.

Точните shift правила ще се уточнят по-късно.

---

# 7. Desktop Web Shift Planner

На Web версията планирането трябва да е максимално лесно и визуално.

Основната идея е **drag & drop**.

Мениджърът вижда:
- работниците;
- датите;
- смените.

След това може да хване работник и да го плъзне върху съответната смяна.

Примерна концепция:

Workers:
- Worker A
- Worker B
- Worker C
- Worker D

↓

Monday / Shift 1
- Worker A
- Worker C

Monday / Shift 2
- Worker B
- Worker D

Целта е да няма сложни форми за всяко назначаване.

Desktop интерфейсът трябва да е възможно най-елементарен и бърз.

---

# 8. Mobile Shift Planning

На телефона не е задължително да се използва същият drag & drop интерфейс.

Mobile версията може да използва:
- избор на дата;
- избор на смяна;
- избор на работник;
- добавяне;
- промяна;
- премахване.

Основният принцип е:
- Desktop → визуален drag & drop.
- Mobile → прост touch workflow.

---

# 9. Login / Logout

Приложението ще има:
- Login;
- Logout;
- идентификация на служителя;
- роли;
- permissions.

Съществуващият Mainty вече използва JWT authentication.

Това е текущата техническа основа и трябва да бъде запазено/развито, след проверка на текущия код.

---

# 10. Attendance / Clock In / Clock Out

Една от основните идеи е системата да записва кога служителят идва на работа и кога си тръгва.

Желаната концепция е автоматично отчитане при връщане в работната среда/мрежа.

Пример:

Устройство
→ налична фирмена мрежа/връзка
→ приложение
→ идентифициране
→ Clock In

При напускане:
→ Clock Out

Трябва да се записват поне:
- служител;
- дата;
- час;
- тип на събитието;
- начин на отчитане;
- sync информация при нужда.

Точният механизъм за автоматично разпознаване трябва да бъде обсъден. Не трябва автоматично да се приема, че само наличието на интернет означава физическо присъствие.

---

# 11. Manual Clock In / Clock Out

Автоматичното отчитане не трябва да е единственият вариант.

Трябва да има ръчно отчитане при:
- проблем с телефона;
- проблем с приложението;
- проблем с мрежата;
- проблем със sync;
- проблем с автоматичното разпознаване;
- друг технически проблем.

Пример:
- Manual Clock In
- Manual Clock Out

Трябва по-късно да се реши:
- кой може да коригира запис;
- дали се изисква manager approval;
- как се пази историята на корекциите.

---

# 12. Offline / Local Database

Мобилните приложения трябва да могат да използват локално съхранение.

Концепция:

Online:
Server → Local database/cache

Offline:
Mobile app → Local database

След възстановяване на връзката:
Local changes → Synchronization → Server

Пример:

Clock In
→ няма мрежа
→ записва се локално
→ мрежата се връща
→ записът се синхронизира.

Данните не трябва да се губят заради временна липса на интернет.

---

# 13. Offline synchronization

Трябва да има ясна разлика между:
- server data;
- local data;
- pending changes;
- synchronized changes;
- conflicts.

Трябва да се проектира какво става при конфликт и кой е source of truth.

Сървърът по принцип трябва да бъде централният source of truth, а локалната база да позволява работа offline и pending operations.

---

# 14. Web версия

Web версията е първата версия.

Тя е особено важна за:
- Production Manager;
- Engineering Manager;
- други мениджъри;
- администрация.

Web версията трябва да е удобна за:
- Shift Planning;
- drag & drop;
- dashboard;
- Maintenance;
- таблици;
- management workflows.

---

# 15. Android приложение

След Web версията трябва да има Android приложение.

То е насочено основно към работниците и служителите, които използват телефон.

Основни мобилни функции:
- Login;
- виждане на собствените смени;
- Clock In;
- Clock Out;
- съобщения;
- notifications;
- други одобрени мобилни функции.

---

# 16. iPhone / iOS приложение

Трябва да има и iPhone/iOS приложение.

Основната функционалност трябва да е аналогична:
- Login;
- смени;
- Clock In;
- Clock Out;
- Messages;
- Notifications.

Интерфейсът може да бъде адаптиран към iOS.

---

# 17. Notifications

Приложението трябва да има push notifications.

Те са особено важни за Android и iPhone.

Пример:

Manager
→ изпраща съобщение
→ системата определя получателите
→ push notification
→ телефон на работника

Така работникът може да бъде информиран без постоянно да отваря приложението.

---

# 18. Messages / Фирмени съобщения

Мениджърите трябва да могат да изпращат фирмени съобщения.

Пример:

„Утре започваме в 07:00.“

Съобщението трябва да може да бъде насочено към:
- конкретен служител;
- няколко конкретни служители;
- Department.

---

# 19. Targeting / Тагване

Идеята за „тагване“ тук означава избор на получатели.

Например:

Message
→ Worker A
→ Worker B
→ Worker C

Само избраните служители получават съобщението/notification.

---

# 20. Department messages

Освен конкретни служители, мениджърът трябва да може да избере Department.

Пример:

Department: Production

→ всички подходящи служители в Production получават съобщението.

Точните Department-и трябва да се вземат от реалната структура на County Food Ingredients.

---

# 21. Message + Notification

Трябва да се разграничат:
- Message — съдържанието, което се пази в системата.
- Push Notification — известяването на телефона.

Концепция:

Manager creates message
→ message saved
→ recipients determined
→ push notification
→ user opens app
→ message is available in app.

Така notification-ът не е единственото място, където се намира информацията.

---

# 22. Възможни бъдещи функции на Messages

Може по-късно да се добавят:
- Inbox;
- прочетено/непрочетено;
- дата;
- подател;
- recipient;
- Department;
- priority;
- история.

Това не е окончателно изискване.

---

# 23. Company Platform

Приложението вече не трябва да се мисли само като Maintenance app.

Концептуално:

CFIA
├── Authentication
├── Dashboard
├── Maintenance
│   ├── Breakdowns
│   ├── Work Orders
│   ├── Shift Tasks
│   └── PPM
├── Shift Planning
├── Attendance
├── Messages
└── Notifications

Това е основната посока.

---

# 24. Incident Reporting — след Version 1

**Incident Reporting** е запазена идея, но НЕ е част от задължителния първи scope.

Статус:

**OPEN QUESTION / FUTURE MODULE**

След като основната версия бъде направена и одобрена, трябва да се обсъди:
- има ли реална нужда;
- какви инциденти се записват;
- кой може да ги докладва;
- кой ги разглежда;
- има ли снимки;
- има ли документи;
- има ли approval;
- има ли workflow;
- има ли notifications;
- има ли връзка с Maintenance.

---

# 25. Производство / продукти / количества / доставки — след Version 1

След като първата версия бъде направена и одобрена, трябва да се говори с отделите.

Тогава може да се разбере дали са нужни:
- Production;
- произведени количества;
- Product tracking;
- входящи материали;
- изходящи продукти;
- доставки;
- откъде са дошли материали;
- къде са изпратени продукти;
- Batch tracking;
- Warehouse;
- Stock;
- други специфични процеси.

Тези модули НЕ трябва да се измислят предварително.

Трябва да се попита фирмата какво реално използва и къде има проблеми.

---

# 26. Подход след Version 1

След първата версия:

Build Version 1
→ реална употреба
→ feedback
→ разговор с отделите
→ откриване на реални проблеми
→ приоритизиране
→ нов модул

Това е предпочитаният подход вместо предварително да се изграждат десетки функции.

---

# 27. Роли

В съществуващия Mainty има:
- Engineer
- EngineeringManager
- Operator
- QA
- ProductionManager

При разширяването може да са нужни други роли, но те трябва да бъдат определени според реалната структура на County Food Ingredients.

Не трябва автоматично да се създават роли без бизнес причина.

---

# 28. Permissions

В бъдеще трябва да се мисли не само „каква е ролята“, а „какво има право да прави“.

Пример:

Production Manager:
- създава shift;
- променя shift;
- вижда графика;
- изпраща съобщения.

Worker:
- вижда собствената си смяна;
- Clock In;
- Clock Out;
- вижда съобщения.

Engineer:
- Maintenance функции.

Точните permissions трябва да бъдат уточнени.

---

# 29. Security

Желаната архитектура е:

React
→ Authorization: Bearer JWT
→ ASP.NET Core JWT Authentication
→ ClaimsPrincipal
→ Roles / Permissions
→ Controller authorization
→ Business logic
→ Database

Frontend guards са за UX.

Backend authorization е реалната защита.

---

# 30. Важна историческа JWT бележка

Старият Mainty използваше:
- X-UserId;
- HttpContext.Items["CurrentUser"].

След миграцията към JWT трябва да се използват:
- Authorization: Bearer JWT;
- JWT claims;
- HttpContext.User;
- ASP.NET Core authorization.

Исторически известен проблем беше WorkOrders `RequireRoles<T>()`, който все още четеше:

```csharp
HttpContext.Items["CurrentUser"]
```

и връщаше:

```text
401 Missing X-UserId
```

Axios interceptor-ът на frontend след това изтриваше accessToken и връщаше потребителя към Login.

Това е важна историческа информация, но текущият GitHub код трябва да се провери преди промяна.

---

# 31. 401 срещу 403

Трябва да се пази разликата:

401:
- няма валидна authentication;
- липсва token;
- token е изтекъл;
- token е невалиден;
- JWT configuration проблем.

403:
- потребителят е authenticated;
- но няма необходимото право/role.

Например Operator може да получи 403 за full WorkOrders list, ако това е правилото.

Не трябва неправилен role denial да бъде връщан като 401, защото frontend може да приеме това като изтекъл JWT и да logout-не потребителя.

---

# 32. UX принцип

Главната цел е:

**Приложението да е по-лесно от сегашния начин на работа.**

Не трябва просто да се дигитализира сложен процес 1:1.

Трябва да се търси най-лесният workflow.

Особено за:
- Shift Planning;
- Attendance;
- Messages;
- Maintenance.

---

# 33. Desktop пример

Production Manager:

Open application
→ Shift Planning
→ вижда графика
→ вижда workers
→ drag worker
→ drop върху shift
→ Save
→ workers виждат промяната.

Това трябва да бъде възможно най-бързо.

---

# 34. Worker пример

Worker:

Open app
→ вижда днешната/следващата смяна
→ вижда важните съобщения
→ Clock In / Clock Out
→ получава notifications.

---

# 35. Attendance audit

Attendance трябва да има история.

Концептуално:
- Employee;
- Date;
- Event;
- Timestamp;
- Automatic/Manual;
- Source;
- Sync status;
- correction information.

Точните полета ще бъдат определени по-късно.

---

# 36. Основни въпроси за Shift Planning

Преди имплементация трябва да се изясни:

1. Колко типа смени има?
2. Какви са часовете?
3. Има ли rotating shifts?
4. Има ли 4-on / 4-off?
5. Има ли overtime?
6. Има ли breaks?
7. Може ли човек да бъде заменен?
8. Кой може да променя графика?
9. Колко напред се планира?
10. Може ли worker да поиска промяна?
11. Нужен ли е manager approval?
12. Как се обработва absence?
13. Как се обработва holiday?
14. Какво става при last-minute промяна?

---

# 37. Основни въпроси за Attendance

1. Как точно се разпознава присъствието?
2. Какво означава „автоматично“?
3. Използва ли се фирмен Wi-Fi?
4. Има ли определена зона?
5. Има ли служебни телефони?
6. Как се предотвратява clock-in от разстояние?
7. Как се коригира грешен запис?
8. Кой може да прави корекция?
9. Нужен ли е manager approval?
10. Какво става offline?
11. Кое време се използва offline?
12. Как се решават sync конфликти?

---

# 38. Основни въпроси за Messages

1. Кой може да изпраща?
2. Към кои Department-и?
3. Може ли конкретен worker?
4. Може ли няколко workers?
5. Има ли priority?
6. Има ли expiry?
7. Има ли read/unread?
8. Може ли edit?
9. Може ли delete?
10. Трябва ли история?
11. Трябва ли attachment?
12. Всяко message ли трябва да изпраща push notification?

---

# 39. Mobile architecture

Желаната концепция е един централен backend:

Web
   Android ----> API ----> Database
   /
iPhone

Business logic трябва да е централизирана в backend.

Web, Android и iPhone не трябва да имат различни версии на една и съща business logic.

---

# 40. Data ownership

Сървърът е централният source of truth за фирмените данни.

Мобилното устройство има локални данни за:
- offline работа;
- cache;
- pending operations;
- sync.

Точните conflict правила трябва да се проектират.

---

# 41. Приоритети

## Priority 1 — Core

- Authentication
- Users
- Roles / permissions
- Dashboard
- Maintenance
- Breakdowns
- Work Orders

## Priority 2 — Shift & Attendance

- Shift Planning
- Worker schedules
- Worker schedule view
- Clock In
- Clock Out
- Manual Clock In/Out
- Offline support
- Synchronization

## Priority 3 — Communication

- Messages
- Targeted messages
- Department targeting
- Employee targeting
- Push notifications

## Priority 4 — Mobile

- Android
- iPhone
- mobile authentication
- mobile shifts
- mobile attendance
- mobile notifications

## Priority 5 — After Version 1

- Incident Reporting
- Production
- Products
- Warehouse
- Stock
- Batch tracking
- други реални нужди от отделите

---

# 42. Какво НЕ е одобрено

Следните идеи НЕ са част от фиксирания scope:

- QR scanning за Maintenance;
- voice description;
- Start/Finish Job;
- engineer checklists;
- други допълнителни Maintenance екстри.

Те могат да се обсъдят някога, но не трябва да се приемат за изисквания.

Също така не са одобрени произволни общи модули като meeting rooms, visitor management и подобни.

---

# 43. Важен принцип за нови модули

След Version 1 първо трябва да се говори с реалните отдели.

Въпросът е:

„Кой процес ви губи най-много време?“

а не:

„Какъв модул можем да добавим?“

След това:

Problem
→ Business requirement
→ UX design
→ Technical design
→ Implementation
→ Testing
→ Deployment
→ Feedback

---

# 44. Текуща цел

Основната цел е да се направи реално полезно приложение за County Food Ingredients, което започва с Mainty/Maintenance основата и постепенно обединява:

**Maintenance + Shift Planning + Attendance + Communication + Mobile**

в една система.

---

# 45. Финална бележка за друг AI

Този документ трябва да бъде прочетен заедно с GitHub repository.

Другият AI трябва:

1. Да прочете документа.
2. Да разгледа GitHub repository.
3. Да провери текущия branch/commit.
4. Да сравни текущия код с документа.
5. Да не приема старите code snippets за актуални.
6. Да провери JWT authentication и authorization.
7. Да провери остатъците от X-UserId / CurrentUser.
8. Да анализира архитектурата преди да променя код.
9. Да работи стъпка по стъпка.
10. Да не прави големи промени без потвърждение.

---

# 46. Prompt за начало с друг AI

Може да му бъде даден този текст:

„Това е проектът Mainty, който ще бъде разширен за County Food Ingredients. Прочети този документ изцяло и след това разгледай GitHub repository. Не променяй код още.

Първо ми обясни:
1. текущата архитектура;
2. как работи authentication/JWT;
3. как работят roles/permissions;
4. как работи Maintenance/WorkOrders;
5. какво вече е направено;
6. какво липсва;
7. дали са останали части от стария X-UserId authentication;
8. как текущият Mainty може да се разшири към новата CFIA концепция;
9. какви архитектурни промени са нужни преди новите модули.

След анализа чакай моето потвърждение. Работи по един файл/стъпка наведнъж.“

---

# END
