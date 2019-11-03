from openpyxl import load_workbook
import pika
import json
import datetime
import re


'''
  Программа считывает из XLSX документа данные по входу и выходу специальностей,
  после этого считывает список видео-файлов и определяет, входит ли время входа
  специалиста в кусок видео. Если входит, то он составляет задание для вырезки куска
  видео0файла со специалистом.
  Есть нюансы:
  1. Время выступления специалиста может затрагивать не только две камеры, но и несколько
     видео с одной камеры (камера делает несколько видео за день, разделение видеофайлов
     может произойти во время выступления специалиста)
  2. Время из абсолютного значения переводится в относительное (в видеофайле время видео 
     начинается с 0).
  ---
  Формат XLSX-файла:
  Специальность | Дата | Кабинет | Время входа | Время выхода |
  Фармация      |   1  |    9    |   9:08:06   |   10:16:48   |
  ---
  Формат файла с видео-файлами (Камера - Корпус - ДатаВремяОт - ДатаВремяДо):
  15-1_Корпус 2 - Локально_Камеры Корпус 2 - Локально_20190701075952_20190701114336_80682625.mp4
  15-1_Корпус 2 - Локально_Камеры Корпус 2 - Локально_20190701114336_20190701154215_80810843.mp4
  15-1_Корпус 2 - Локально_Камеры Корпус 2 - Локально_20190701154215_20190701195143_80948554.mp4
  15-1_Корпус 2 - Локально_Камеры Корпус 2 - Локально_20190701195143_20190701195959_81086170.mp4
  ---
'''

''' Настройки '''
xlsx_times_file = './in/times.xlsx'
videos_file = './in/files.txt'


def read_times_file(filename: 'str') -> '[{spec,date,cab,dt_from,dt_to}]':
	''' Читает файл с временем входа/выхода экспертов и возвращает их '''
	times = []
	wb = load_workbook(filename)
	ws = wb.active
	for row in ws.rows:
		if (type(row[3].value) == datetime.time) and (type(row[4].value) == datetime.time):
			spec_name = row[0].value
			day = row[1].value
			cab = row[2].value
			time_in = row[3].value
			dt_from = datetime.datetime(2019, 7, day, time_in.hour, time_in.minute, time_in.second)
			time_out = row[4].value
			dt_to = datetime.datetime(2019, 7, day, time_out.hour, time_out.minute, time_out.second)
			times.append({ 'spec': spec_name, 'cab': cab, 'dt_from': dt_from, 'dt_to': dt_to })
	return times


def get_time(filename: 'str') -> '(datetime_from, datetime_to)':
	''' Функция выбирает из имени файла абсолютные время начала и время окончания файла 
		и возвращает кортеж из двух datetime '''
	result = re.search(r'_(\d\d\d\d)(\d\d)(\d\d)(\d\d)(\d\d)(\d\d)_(\d\d\d\d)(\d\d)(\d\d)(\d\d)(\d\d)(\d\d)_\d+\.mp4', filename)
	if result:
		y1,m1,d1,h1,min1,s1 = int(result.group(1)),int(result.group(2)),int(result.group(3)),int(result.group(4)),int(result.group(5)),int(result.group(6))
		y2,m2,d2,h2,min2,s2 = int(result.group(7)),int(result.group(8)),int(result.group(9)),int(result.group(10)),int(result.group(11)),int(result.group(12))
		return (datetime.datetime(y1,m1,d1,h1,min1,s1), datetime.datetime(y2,m2,d2,h2,min2,s2))
	return (None, None)


def get_camera_num(filename: 'str') -> 'str':
	''' Возвращает номер камеры '''
	return re.sub(r'^\d+-(\d+)_.*$', r'\1', filename)


def read_video_files(filename):
	lines = []
	with open(filename,'r',encoding='utf-8') as file:
		in_lines = file.readlines()
		for line in in_lines:
			filename = line.rstrip()
			(dt_from, dt_to) = get_time(filename)
			lines.append({ 'filename': filename, 'dt_from': dt_from, 'dt_to': dt_to })
	return lines


def convert_timedelta(timedelta):
	''' Конвертирует разницу времени в строку 'ЧЧ:ММ:СС' '''
	days, seconds = timedelta.days, timedelta.seconds
	hours = days * 24 + seconds // 3600
	hours = '0%s' % str(hours) if hours < 10 else str(hours)
	minutes = (seconds % 3600) // 60
	minutes = '0%s' % str(minutes) if minutes < 10 else str(minutes)
	seconds = (seconds % 60)
	seconds = '0%s' % str(seconds) if seconds < 10 else str(seconds)
	return '%s:%s:%s' % (hours, minutes, seconds)


def if_cabinet(cab: 'int', filename: 'str'):
	pattern = r'^%s-' % cab
	return (True if re.search(pattern, filename) else False)


def if_time(dt_from, dt_to, dt_from_v, dt_to_v):
	''' Функция находит вхождение времени выступления
		специалиста и времени видеофайла
		dt_from, dt_to - специалиста
	    dt_from_v, dt_to_v - видеофайла '''
	return (dt_from_v <= dt_from <= dt_to_v) or (dt_from_v <= dt_to <= dt_to_v)


def make_timing(dt_from, dt_to, dt_from_v, dt_to_v: 'datetime') -> '("00:00:00","00:00:01")':
	''' Функция определяет относительное время 
	    выступления специалиста внутри видео '''
	start_delta = (dt_from - dt_from_v) if (dt_from_v <= dt_from <= dt_to_v) else (dt_from_v - dt_from_v)
	stop_delta = (dt_to - dt_from_v) if (dt_from_v <= dt_to <= dt_to_v) else (dt_to_v - dt_from_v)
	start_time  = convert_timedelta(start_delta)
	stop_time = convert_timedelta(stop_delta)
	return (start_time, stop_time)



def message():
	''' Возвращает стандартное задание для обработки '''
	return {
        "type": "task",
        "task_id": "ABC",
        "comment": "",

        "ftp_in_host": "192.168.0.3",
        "ftp_in_port": 21,
        "ftp_in_username": "admin",
        "ftp_in_password": "admin",
        "ftp_in_dir": "in",
        "ftp_in_filename": "",

        "ftp_out_host": "192.168.0.3",
        "ftp_out_port": 21,
        "ftp_out_username": "admin",
        "ftp_out_password": "admin",
        "ftp_out_dir": "out",
        "ftp_out_filename": "ГБПОУ_СМК_ИМ_Н_ЛЯПИНОЙ-АД-1э-камера-1-03.07.2018-1.mp4",
        
        "ffmpeg_video_codec": "h264",
        "ffmpeg_video_bitrate": "500k",
        "ffmpeg_audio_codec": "aac",
        "ffmpeg_audio_bitrate": "128k",
        "ffmpeg_vf": "scale=1024:768",
        "ffmpeg_from": "",
        "ffmpeg_to": ""
    }


if __name__ == '__main__':
	times = read_times_file(xlsx_times_file)
	videos = read_video_files(videos_file)
	
	tasks = [] # Задания для обработки
	for spec_time in times:
		''' Для каждой специальности '''
		dt_from = spec_time['dt_from']
		dt_to = spec_time['dt_to']
		cab = spec_time['cab']
		spec = spec_time['spec']
		''' Проходим по всем видеофайлам '''
		duplicates = {}
		for video in videos:
			filename = video['filename']
			dt_from_v = video['dt_from']
			dt_to_v = video['dt_to']
			''' Если вхождение найдено '''
			if if_cabinet(cab, filename) and if_time(dt_from, dt_to, dt_from_v, dt_to_v):
				date = dt_from.strftime('%d.%m.%Y')
				(start_time, stop_time) = make_timing(dt_from, dt_to, dt_from_v, dt_to_v)
				cam = get_camera_num(filename)
				task = message()
				task['ftp_in_filename'] = filename
				task['ffmpeg_from'] = start_time
				task['ffmpeg_to'] = stop_time
				out_filename = "ГБПОУ_СМК_ИМ_Н_ЛЯПИНОЙ-%s-1э-камера-%s-%s" % (spec, cam, date)
				if out_filename in duplicates:
					duplicates[out_filename] += 1
					task['ftp_out_filename'] = out_filename + ("_%s.mp4" % str(duplicates[out_filename]))
				else:
					duplicates[out_filename] = 0
					task['ftp_out_filename'] = out_filename + ".mp4"
				tasks.append(task)

	''' Публикуем задания '''
	connection = pika.BlockingConnection(pika.URLParameters('amqp://TZBgaW3bDtYXRno5:N0uArziqiLWTsfsn@192.168.0.3:5672/%2F'))
	channel = connection.channel()
	channel.queue_declare(queue='video_processing_tasks', durable=True)
	for task in tasks:
		channel.basic_publish(exchange='',
                          routing_key='video_processing_tasks',
                          body=json.dumps(task),
                          properties=pika.BasicProperties(delivery_mode=2))  # make message persistent
		print("Task sent!")
	connection.close()


