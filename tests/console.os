Перем юТест;

Функция ПолучитьСписокТестов(ЮнитТестирование) Экспорт

	юТест = ЮнитТестирование;

	ВсеТесты = Новый Массив;
	ВсеТесты.Добавить("ТестДолжен_ПроверитьЧтоСтандартныйПотокВводаЭтоПоток");
	ВсеТесты.Добавить("ТестДолжен_ПроверитьТаймаутЧтенияСтандартногоПотокВвода");
	#Если Не Linux Тогда
	// TODO: https://github.com/EvilBeaver/OneScript/issues/1255
	ВсеТесты.Добавить("ТестДолжен_ПроверитьПередачуДанныхВСкриптЧерезСтандартныйПотокВвода");
	#КонецЕсли
	ВсеТесты.Добавить("ТестДолжен_ПроверитьПеренаправлениеВывода");

	Возврат ВсеТесты;

КонецФункции

Процедура ТестДолжен_ПроверитьЧтоСтандартныйПотокВводаЭтоПоток() Экспорт
	
	ПотокВвода = Консоль.ОткрытьСтандартныйПотокВвода();
	юТест.ПроверитьРавенство(Тип("Поток"), ТипЗнч(ПотокВвода), "Ошибка открытия стандартного потока ввода");

КонецПроцедуры

Процедура ТестДолжен_ПроверитьТаймаутЧтенияСтандартногоПотокВвода() Экспорт

	ПутьКОскрипт = ОбъединитьПути(КаталогПрограммы(), "oscript.dll");
	
	КодСкрипта = "ВходящийПоток = Консоль.ОткрытьСтандартныйПотокВвода();
	             |ВходящийПоток.ТаймаутЧтения = 100;
	             |Чтение = Новый ЧтениеТекста();
	             |Чтение.Открыть(ВходящийПоток);
	             |Попытка 
				 |	Сообщить(СокрЛП(Чтение.Прочитать()));
				 |Исключение
				 |	Сообщить(ИнформацияобОшибке().ПодробноеОписаниеОшибки());
				 |	ВызватьИсключение;
				 |КонецПопытки;";

	ТекстСкрипта = Новый ТекстовыйДокумент();
	ТекстСкрипта.УстановитьТекст(КодСкрипта);

	ВремФайл = ПолучитьИмяВременногоФайла("os");

	ТекстСкрипта.Записать(ВремФайл);

	ТестовыеДанные = "";

	ИсполняемаяКоманда = СтрШаблон("dotnet ""%1"" ""%2""", ПутьКОскрипт, ВремФайл);
	#Если Windows Тогда
		СтрокаЗапуска = СтрШаблон("cmd /c ""%1""", ИсполняемаяКоманда);
	#Иначе
		СтрокаЗапуска = СтрШаблон("sh -c '%1'", ИсполняемаяКоманда);
	#КонецЕсли
	Процесс = СоздатьПроцесс(СтрокаЗапуска, , Истина);
	Процесс.Запустить();

	МаксимумОжидания = 1000;
	ИнтервалОжидания = 100;
	ВсегоОжидание = 0;
	Пока НЕ Процесс.Завершен Цикл
		Приостановить(ИнтервалОжидания);
		ВсегоОжидание = ВсегоОжидание + ИнтервалОжидания;
		Если ВсегоОжидание >= МаксимумОжидания Тогда
			Процесс.Завершить();
			юТест.ТестПровален("Ошибка чтения пустого стандартного потока ввода. Истекло время ожидания.");
		КонецЕсли;
	КонецЦикла;

	ВыводКоманды = СокрЛП(Процесс.ПотокВывода.Прочитать());
	
	УдалитьФайлы(ВремФайл);
	Сообщить(ВыводКоманды);
	юТест.ПроверитьРавенство(ВыводКоманды, ТестовыеДанные, "Ошибка чтения пустого стандартного потока ввода.");

КонецПроцедуры

Процедура ТестДолжен_ПроверитьПередачуДанныхВСкриптЧерезСтандартныйПотокВвода() Экспорт

	ПутьКОскрипт = ОбъединитьПути(КаталогПрограммы(), "oscript.dll");
	
	КодСкрипта = "Чтение = Новый ЧтениеТекста();
	             |Чтение.Открыть(Консоль.ОткрытьСтандартныйПотокВвода());
	             |Сообщить(СокрЛП(Чтение.Прочитать()));
	             |";

	ТекстСкрипта = Новый ТекстовыйДокумент();
	ТекстСкрипта.УстановитьТекст(КодСкрипта);

	ВремФайл = ПолучитьИмяВременногоФайла("os");

	ТекстСкрипта.Записать(ВремФайл);

	ТестовыеДанные = "12346";

	ИсполняемаяКоманда = СтрШаблон("echo %1 | dotnet %2 %3", ТестовыеДанные, ПутьКОскрипт, ВремФайл);
	#Если Windows Тогда
		СтрокаЗапуска = СтрШаблон("cmd /c ""%1""", ИсполняемаяКоманда);
	#Иначе
		СтрокаЗапуска = СтрШаблон("sh -c '%1'", ИсполняемаяКоманда);
	#КонецЕсли

	Процесс = СоздатьПроцесс(СтрокаЗапуска, , Истина);
	Процесс.Запустить();

	Пока НЕ Процесс.Завершен Цикл
		Приостановить(100);
	КонецЦикла;

	ВыводКоманды = СокрЛП(Процесс.ПотокВывода.Прочитать());

	УдалитьФайлы(ВремФайл);

	юТест.ПроверитьРавенство(ВыводКоманды, ТестовыеДанные, "Ошибка чтения стандартного потока ввода.");

КонецПроцедуры

Процедура ТестДолжен_ПроверитьПеренаправлениеВывода() Экспорт
	
	ВФ = ПолучитьИмяВременногоФайла();
	Поток = ФайловыеПотоки.ОткрытьДляЗаписи(ВФ);
	Консоль.УстановитьПотокВывода(Поток);
	Попытка
		Сообщить("Привет мир!");
	Исключение
		// что-то пошло не так
		Консоль.УстановитьПотокВывода(Консоль.ОткрытьСтандартныйПотокВывода());
		ВызватьИсключение;
	КонецПопытки;
	
	Поток.Закрыть();
	Консоль.УстановитьПотокВывода(Консоль.ОткрытьСтандартныйПотокВывода());

	Чтение = Новый ЧтениеТекста(ВФ, Консоль.КодировкаВыходногоПотока);
	Текст = Чтение.Прочитать();
	Чтение.Закрыть();

	УдалитьФайлы(ВФ);

	юТест.ПроверитьРавенство("Привет мир!", СокрЛП(Текст));

КонецПроцедуры