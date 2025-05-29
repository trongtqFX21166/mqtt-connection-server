import os
import psycopg2
import logging
from typing import List, Dict, Any, Optional

# Thiết lập logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler("emqx_test.log", encoding='utf-8'),
        logging.StreamHandler()
    ]
)
logger = logging.getLogger("db_utils")

class DatabaseConnection:
    """Quản lý kết nối đến PostgreSQL database"""
    
    @staticmethod
    def get_connection():
        """Tạo kết nối đến database dựa trên biến môi trường"""
        try:
            conn = psycopg2.connect(
                host=os.environ.get('DB_HOST', 'localhost'),
                port=os.environ.get('DB_PORT', '5432'),
                database=os.environ.get('DB_NAME', 'your_db'),
                user=os.environ.get('DB_USER', 'postgres'),
                password=os.environ.get('DB_PASSWORD', 'postgres')
            )
            return conn
        except Exception as e:
            logger.error(f"Lỗi kết nối database: {str(e)}")
            return None

def query_users(password: str = None, limit: int = 100) -> List[Dict[str, Any]]:
    """
    Truy vấn người dùng từ database
    
    Args:
        password: Mật khẩu để lọc người dùng
        limit: Số lượng user tối đa cần lấy
        
    Returns:
        List[Dict]: Danh sách user với id và phone
    """
    users = []
    conn = None
    
    try:
        # Sử dụng mật khẩu từ tham số hoặc từ biến môi trường
        if password is None:
            password = os.environ.get("USER_PASSWORD", "AB5717028198B5EF")
        
        conn = DatabaseConnection.get_connection()
        if not conn:
            logger.error("Không thể kết nối đến database")
            return []
        
        cursor = conn.cursor()
        
        # Truy vấn người dùng dựa trên mật khẩu
        query = 'SELECT id, phone FROM "user" WHERE password = %s LIMIT %s'
        logger.info(f"Truy vấn người dùng với mật khẩu: {password[:4]}***")
        cursor.execute(query, (password, limit))
        
        for user_id, phone in cursor.fetchall():
            users.append({
                'id': user_id,
                'phone': phone
            })
        
        logger.info(f"Đã truy vấn {len(users)} người dùng từ database")
        return users
        
    except Exception as e:
        logger.error(f"Lỗi khi truy vấn dữ liệu: {str(e)}")
        return []
    finally:
        if conn:
            conn.close()

def load_users_from_file(filename: str = "cached_users.json") -> List[Dict[str, Any]]:
    """
    Đọc thông tin người dùng từ file cache
    
    Args:
        filename: Đường dẫn đến file cache
        
    Returns:
        List[Dict]: Danh sách user hoặc list rỗng nếu không tìm thấy file
    """
    import json
    try:
        if os.path.exists(filename):
            with open(filename, 'r', encoding='utf-8') as f:
                data = json.load(f)
            logger.info(f"Đã đọc {len(data)} người dùng từ file {filename}")
            return data
        return []
    except Exception as e:
        logger.error(f"Lỗi khi đọc file {filename}: {str(e)}")
        return []

def save_users_to_file(users: List[Dict[str, Any]], filename: str = "cached_users.json") -> bool:
    """
    Lưu thông tin người dùng vào file để tái sử dụng
    
    Args:
        users: Danh sách user cần lưu
        filename: Đường dẫn file đầu ra
        
    Returns:
        bool: True nếu lưu thành công, False nếu thất bại
    """
    import json
    try:
        with open(filename, 'w', encoding='utf-8') as f:
            json.dump(users, f, indent=2)
        logger.info(f"Đã lưu {len(users)} người dùng vào file {filename}")
        return True
    except Exception as e:
        logger.error(f"Lỗi khi lưu file {filename}: {str(e)}")
        return False

def get_users(use_cache: bool = True, limit: int = 100) -> List[Dict[str, Any]]:
    """
    Lấy danh sách người dùng từ cache hoặc database
    
    Args:
        use_cache: Sử dụng dữ liệu cache nếu có
        limit: Số lượng user tối đa
        
    Returns:
        List[Dict]: Danh sách user
    """
    users = []
    
    # Thử đọc từ cache trước
    if use_cache:
        users = load_users_from_file()
    
    # Nếu không có cache hoặc cache rỗng, truy vấn từ database
    if not users:
        users = query_users(limit=limit)
        if users:
            save_users_to_file(users)
    
    if not users:
        logger.error("Không thể lấy dữ liệu người dùng từ database và cache")
        raise Exception("Không có dữ liệu người dùng để test")
    
    return users

# Chúng ta sẽ luôn sử dụng dữ liệu chính xác từ database
