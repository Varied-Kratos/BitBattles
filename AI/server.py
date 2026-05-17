import socket
import random

HOST = '127.0.0.1'
PORT = 5005

def generate_random_layout():
    units = []
    # Сетка 5x5 = 25 клеток
    for _ in range(25):
        # 10% шанс спавна юнита, чтобы не перегружать доску
        if random.random() < 0.15:
            unit_type = random.randint(1, 3) # Рыцарь, Лучник или Маг
            level = random.randint(1, 3)      # Уровень от 1 до 3
            units.append(f"{unit_type}:{level}")
        else:
            units.append("0:0")
    return ",".join(units)

def start_test_server():
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        s.bind((HOST, PORT))
        s.listen()
        print(f"TEST SERVER ONLINE | PORT {PORT}")
        print("Waiting for Unity to connect...")

        while True:
            conn, addr = s.accept()
            with conn:
                data = conn.recv(1024)
                if not data:
                    break
                
                msg = data.decode()
                print(f"Unity says: {msg}")

                # Генерируем случайную расстановку с уровнями
                response = generate_random_layout()
                print(f"Sending layout: {response}")
                
                conn.sendall(response.encode())

if __name__ == "__main__":
    start_test_server()