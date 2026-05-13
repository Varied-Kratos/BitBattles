import socket
from ga_manager import GAManager

HOST = '127.0.0.1'
PORT = 5005

def start_server():
    # Инициализируем мозги ИИ
    ga = GAManager(pop_size=10, max_elixir=10)
    current_ind_idx = 0

    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        s.bind((HOST, PORT))
        s.listen()
        print(f"BITBATTLES AI SERVER ONLINE | PORT {PORT}")

        while True:
            conn, addr = s.accept()
            with conn:
                data = conn.recv(4096)
                if not data: break
                
                message = data.decode()
                try:
                    # Парсим "RESULT:фитнес|доска"
                    parts = message.split('|')
                    raw_fit = float(parts[0].split(':')[1])
                    
                    # Применяем фитнес со штрафом за эликсир
                    cost, final_fit = ga.set_fitness(current_ind_idx, raw_fit)
                    
                    print(f"IND {current_ind_idx} | Raw: {raw_fit:.1f} | Final: {final_fit:.1f} | Elixir: {cost}/{ga.max_elixir}")
                    
                    current_ind_idx += 1
                except (IndexError, ValueError):
                    print("Initial request received (New Battle)")

                # Если прошли всю популяцию — эволюционируем
                if current_ind_idx >= ga.pop_size:
                    ga.evolve()
                    current_ind_idx = 0

                # Отправляем новый лейаут Unity
                next_layout = ",".join(map(str, ga.get_individual(current_ind_idx)))
                conn.sendall(next_layout.encode())

if __name__ == "__main__":
    start_server()