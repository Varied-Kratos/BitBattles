import socket
import numpy as np

def start_server():
    host = '127.0.0.1'
    port = 5005

    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.bind((host, port))
        s.listen()
        print(f"QBIT Server online! Ждем данные на порту {port}...")
        
        while True:
            conn, addr = s.accept()
            with conn:
                data = conn.recv(4096) # Буфер побольше для 100 чисел
                if not data: break
                
                # Декодируем строку и превращаем обратно в массив float
                raw_string = data.decode()
                board_array = np.fromstring(raw_string, sep=',')
                
                print(f"Получена доска! Размер: {len(board_array)}")
                print(f"Первые 5 значений: {board_array[:5]}")
                
                # Тут будет вызов твоей модели PyTorch
                # response = model.predict(board_array)
                response = "15" # Заглушка: ИИ говорит ходить на 15-ю клетку
                
                conn.sendall(response.encode())

if __name__ == "__main__":
    start_server()